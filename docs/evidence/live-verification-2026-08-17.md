# Live verification — 17 August 2026

Environment: Windows 11, Docker Desktop Engine 28.4.0, Docker Compose, Camunda 8.9.16, PostgreSQL 16, .NET 8 containers, Node 24 build container.

## Result

- Clean empty-volume startup: passed.
- Camunda deployment: BPMN, DMN, and form accepted; deployment key returned.
- Worker registration: all 11 job types registered.
- `auto-approve`: reached `Approved` with `LOW_RISK` audit reason.
- `manual-review`: reached `ManualReview` with an active human task path.
- `decline`: reached `Declined`.
- `transient-failure`: failed provider checks twice and recovered to `Approved` through retries.
- `incident`: created five active `JOB_NO_RETRIES` Camunda incidents and stayed `ChecksRunning`.
- Idempotency replay: returned the same application ID.
- Withdrawal: moved a manual-review application to `Withdrawn` with contiguous audit sequences `1,2,3,4`.
- Dashboard: returned HTTP 200 and proxied application API data.
- Web container: ran as non-root UID/GID 101.

## Automated verification

- Backend: 59/59 tests passed.
- Frontend: 3/3 tests passed.
- Frontend release build: passed.
- Docker images: API, worker, and web built successfully.
- Docker services: PostgreSQL, Camunda, API, worker, and web healthy.
- Kubernetes Kustomize render: passed.
- Azure Bicep compile: passed without diagnostics using Bicep 0.45.15.

## Defects found by live testing

1. API and worker initially attempted empty-schema creation concurrently. Compose now starts the worker only after API readiness.
2. Withdrawal could race the Camunda completion worker and collide on an audit sequence. The service now reloads after correlation, recovers if the worker wins, uses the persisted maximum sequence, and treats duplicate terminal completion idempotently. Regression tests cover both cases.
