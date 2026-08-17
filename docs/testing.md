# Testing and verification

## Automated layers

| Layer | Coverage focus |
|---|---|
| Domain | validation, transition rules, decision-rule oracle |
| Infrastructure | idempotency, persistence, audit ordering, workflow gateway contracts |
| Workflow | BPMN graph, job types, DMN rules, form binding, retry declarations |
| API | status codes, problem details, privacy, correlation, filtering, health |
| Worker | deterministic scenarios, retry failures, idempotent side effects, completion |
| Frontend | dashboard rendering, detail interaction, create-application workflow |
| Live stack | real Camunda deployment, all scenarios, incidents, withdrawal, proxy health |

## Backend

```powershell
dotnet restore AngleFlow.sln
dotnet test AngleFlow.sln --configuration Release --no-restore
```

The latest local run passed 59 tests: 14 domain, 8 infrastructure, 6 workflow, 10 API, and 21 worker tests.

## Frontend

```powershell
Push-Location web
npm ci
npm test
npm run build
Pop-Location
```

The latest local run passed 3 Vitest tests and produced a Vite release bundle of about 207 kB JavaScript (about 65 kB gzip).

## Delivery assets

```powershell
docker compose config --quiet
docker compose build api worker web
kubectl kustomize deploy/k8s
az bicep build --file infra/azure/main.bicep
```

The Bicep template compiled with Bicep CLI 0.45.15 without diagnostics. Kubernetes resources rendered successfully with kubectl 1.32.2. The web runtime was checked inside Docker as non-root UID 101.

## Live stack

```powershell
docker compose up -d --build --wait
./scripts/smoke-test.ps1
```

The live test is intentionally separate from unit tests because it proves the executable BPMN and DMN resources are accepted by Camunda, workers can activate jobs, retries lead to recovery or incidents, the API can correlate withdrawal, and Nginx can proxy the real backend.

The recorded result is in [live-verification-2026-08-17.md](evidence/live-verification-2026-08-17.md).
