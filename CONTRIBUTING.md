# Contributing

1. Create a focused branch.
2. Add or update a failing test before changing behavior.
3. Run backend tests, frontend tests/build, and delivery-asset validation.
4. Keep examples synthetic and never commit credentials or personal data.
5. Explain workflow-model changes and their migration implications in the pull request.

Useful checks:

```powershell
dotnet test AngleFlow.sln
Push-Location web
npm test
npm run build
Pop-Location
docker compose config --quiet
kubectl kustomize deploy/k8s
```

Use conventional, imperative commit subjects such as `feat: add review escalation` or `fix: preserve audit ordering`.
