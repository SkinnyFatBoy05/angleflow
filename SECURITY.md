# Security policy

## Supported versions

This is a portfolio project and only the latest `main` branch is maintained.

## Reporting a vulnerability

Do not open a public issue containing exploit details, credentials, personal data, or access tokens. Use GitHub's private vulnerability-reporting feature for this repository. Include the affected commit, reproduction steps, impact, and any suggested mitigation.

## Security posture

- All committed records and examples are synthetic.
- Secrets are supplied through environment variables or Kubernetes Secrets; no live credentials belong in the repository.
- Public API responses deliberately omit applicant identity and income fields.
- Containers run as non-root users and Kubernetes workloads drop Linux capabilities.
- CORS is restricted by configuration, API errors use safe problem details, and correlation IDs avoid exposing stack traces.
- Dependency updates are monitored through Dependabot.

The local Compose environment uses development credentials and Camunda's unprotected local API mode. Never expose it to an untrusted network. A production deployment must use TLS, OIDC/authorization, a secret manager, controlled migrations, network policies, backups, monitoring, and an independent security review.
