# Azure deployment baseline

This repository provides compile-verified infrastructure and render-verified Kubernetes application manifests. It has not created Azure resources; deployment requires your own subscription, budget, DNS, identity provider, and security review.

## Provisioned resources

`infra/azure/main.bicep` creates:

- Azure Kubernetes Service with Azure CNI Overlay, Cilium, workload identity, managed identity, stable upgrades, and Container Insights;
- Azure Container Registry and `AcrPull` assignment for the AKS kubelet identity;
- a VNet with separate AKS and PostgreSQL subnets;
- PostgreSQL Flexible Server 16 with public access disabled, private DNS, backups, and an `angleflow` database;
- Key Vault with RBAC, soft delete, and production purge protection; and
- Log Analytics.

Validate and preview before deployment:

```powershell
az bicep build --file infra/azure/main.bicep
az deployment group what-if `
  --resource-group YOUR_RESOURCE_GROUP `
  --template-file infra/azure/main.bicep `
  --parameters environment=dev postgresAdministratorPassword='SUPPLY-SECURELY'
```

Microsoft's [PostgreSQL Bicep quickstart](https://learn.microsoft.com/en-us/azure/postgresql/development/create-server-bicep) describes the flexible-server deployment flow. Review the generated plan and regional SKU availability before creating resources.

## Container images

Build inside ACR or in a trusted CI runner, tag with an immutable commit SHA, and update `deploy/k8s/kustomization.yaml`:

```powershell
az acr build --registry YOUR_ACR --image angleflow-api:GIT_SHA --file src/AngleFlow.Api/Dockerfile .
az acr build --registry YOUR_ACR --image angleflow-worker:GIT_SHA --file src/AngleFlow.Worker/Dockerfile .
az acr build --registry YOUR_ACR --image angleflow-web:GIT_SHA --file web/Dockerfile .
```

Do not deploy mutable `latest` tags to production.

## Camunda on AKS

Camunda recommends Helm/Kubernetes for production Self-Managed installations. Chart 14.x matches Camunda 8.9; this project was checked against chart 14.7.0. For a development cluster only, the official quick-install shape is:

```powershell
kubectl create namespace orchestration
helm repo add camunda https://helm.camunda.io
helm repo update
helm upgrade --install camunda camunda/camunda-platform `
  --version 14.7.0 `
  --namespace orchestration `
  --set orchestration.exporters.rdbms.enabled=true `
  --set orchestration.clusterSize=1 `
  --set orchestration.partitionCount=1 `
  --set orchestration.replicationFactor=1 `
  --set-string 'orchestration.env[0].name=CAMUNDA_PERSISTENT_SESSIONS_ENABLED' `
  --set-string 'orchestration.env[0].value=false'
```

That single-broker H2 configuration is not production. Follow Camunda's [production Helm installation guide](https://docs.camunda.io/docs/self-managed/deployment/helm/install/production/) for multi-zone topology, external storage, TLS, OIDC, secrets, retention, monitoring, and backups.

## Application secrets

`deploy/k8s/secret.example.yaml` documents required keys but is not part of Kustomize. Create `angleflow-secrets` through Key Vault + Secrets Store CSI Driver or another approved secret controller. The app supports `NONE`, `BASIC`, and OAuth Camunda SDK authentication through environment variables.

For a temporary development namespace only:

```powershell
kubectl create secret generic angleflow-secrets `
  --namespace angleflow `
  --from-literal='ConnectionStrings__AngleFlow=YOUR_CONNECTION_STRING' `
  --from-literal='CAMUNDA_BASIC_AUTH_USERNAME=demo' `
  --from-literal='CAMUNDA_BASIC_AUTH_PASSWORD=YOUR_PASSWORD'
```

## Deploy the application

Update registry names and immutable tags, then render and review:

```powershell
kubectl kustomize deploy/k8s
kubectl apply -k deploy/k8s
kubectl rollout status deployment/api -n angleflow
kubectl rollout status deployment/worker -n angleflow
kubectl rollout status deployment/web -n angleflow
```

The included web service is a simple `LoadBalancer` for demonstration. A production environment should place it behind an approved ingress/gateway, WAF, TLS certificate, DNS record, and network policy.
