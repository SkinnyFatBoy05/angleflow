# Operations runbook

## Start and inspect

```powershell
docker compose up -d --build --wait
docker compose ps
docker compose logs api worker --tail 200
```

Healthy services are `postgres`, `orchestration`, `api`, `worker`, and `web`. `camunda-data-init` exits successfully after preparing its volume.

## Health and smoke checks

```powershell
Invoke-RestMethod http://localhost:8088/health/ready
Invoke-WebRequest http://localhost:3000 -UseBasicParsing
./scripts/smoke-test.ps1
```

The smoke test expects approval, manual review, decline, retry recovery, active incidents, idempotent replay, withdrawal, and contiguous audit sequences.

## Investigate an application

```powershell
$id = 'APPLICATION-GUID'
Invoke-RestMethod "http://localhost:8088/api/applications/$id"
Invoke-RestMethod "http://localhost:8088/api/applications/$id/timeline"
```

Use Operate at <http://localhost:8080/operate> to inspect process tokens and incidents. Use Tasklist at <http://localhost:8080/tasklist> for manual-review work. Local credentials are `demo` / `demo`.

## Common failures

### API is unhealthy

```powershell
docker compose logs api postgres --tail 200
docker compose ps
```

Check PostgreSQL health and `ConnectionStrings__AngleFlow`. On an empty local environment, the API initializes the schema before the worker starts.

### Worker has not registered jobs

```powershell
docker compose logs worker --tail 250
```

Look for a workflow deployment key followed by `Registered 11 Camunda job workers`. Confirm `CAMUNDA_REST_ADDRESS` and authentication variables.

### A case is stuck in `ChecksRunning`

Search active incidents:

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:8080/v2/incidents/search `
  -ContentType application/json `
  -Body '{}'
```

An `incident` scenario deliberately produces `JOB_NO_RETRIES`. Other scenarios should be checked against worker exception logs and Camunda Operate.

### Manual review is waiting

That is expected for `manual-review`. Complete the task in Tasklist with `manualDecision` set to `APPROVE` or `DECLINE` and provide `manualReasonCode`.

## Data reset

The following deletes only AngleFlow's local Compose containers and named volumes:

```powershell
docker compose down --volumes --remove-orphans
docker compose up -d --build --wait
```

Do not use this procedure for a shared or production environment.

## Production handoff checklist

- Apply reviewed EF Core migrations from a dedicated deployment job.
- Promote BPMN, DMN, and forms once per release, then pin the intended process version.
- Configure TLS and OIDC; never expose Camunda's unprotected API mode.
- Store credentials in Key Vault and mount them through a managed identity integration.
- Configure supported external Camunda storage, retention, backups, and restore drills.
- Add OpenTelemetry export, alert thresholds, dashboards, and log retention policies.
- Run capacity, failover, and dependency-degradation tests before production traffic.
