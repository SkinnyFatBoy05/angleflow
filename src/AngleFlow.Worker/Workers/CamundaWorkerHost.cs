using System.Text.Json;
using AngleFlow.Infrastructure.Services;
using Camunda.Orchestration.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AngleFlow.Worker.Workers;

public sealed record CompletionWorkerInput(
    string ApplicationId,
    RiskDecisionVariables? RiskDecision,
    string? ManualDecision);

public sealed record WorkerHeaders(string? Template, string? Outcome);

public sealed class CamundaWorkerHost(
    CamundaClient client,
    IServiceScopeFactory scopeFactory,
    ILogger<CamundaWorkerHost> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RegisterCheckWorkers();
        RegisterOutcomeWorkers();
        logger.LogInformation("Registered {WorkerCount} Camunda job workers", WorkerDefinitions.JobTypes.Count);
        return client.RunWorkersAsync(ct: stoppingToken);
    }

    private void RegisterCheckWorkers()
    {
        client.CreateJobWorker(Config("validate-application"), async (job, ct) =>
            await InScope(async services =>
            {
                var input = Required(job.GetVariables<WorkerApplicationInput>());
                return await services.GetRequiredService<CheckWorkerHandlers>().ValidateAsync(input, ct);
            }));

        client.CreateJobWorker(Config("identity-check"), async (job, ct) =>
            await RunCheck(job, "identity", (handlers, input) => handlers.IdentityAsync(input, ct), ct));
        client.CreateJobWorker(Config("credit-check"), async (job, ct) =>
            await RunCheck(job, "credit", (handlers, input) => handlers.CreditAsync(input, ct), ct));
        client.CreateJobWorker(Config("affordability-check"), async (job, ct) =>
            await RunCheck(job, "affordability", (handlers, input) => handlers.AffordabilityAsync(input, ct), ct));
        client.CreateJobWorker(Config("fraud-check"), async (job, ct) =>
            await RunCheck(job, "fraud", (handlers, input) => handlers.FraudAsync(input, ct), ct));
        client.CreateJobWorker(Config("asset-check"), async (job, ct) =>
            await RunCheck(job, "asset", (handlers, input) => handlers.AssetAsync(input, ct), ct));
    }

    private void RegisterOutcomeWorkers()
    {
        client.CreateJobWorker(Config("persist-decision"), async (job, ct) =>
        {
            await InScope(async services =>
            {
                await services.GetRequiredService<OutcomeWorkerHandlers>()
                    .PersistDecisionAsync(Required(job.GetVariables<DecisionWorkerInput>()), ct);
                return true;
            });
        });

        client.CreateJobWorker(Config("create-offer"), async (job, ct) =>
            await InScope(services => services.GetRequiredService<OutcomeWorkerHandlers>()
                .CreateOfferAsync(Required(job.GetVariables<ApplicationWorkerInput>()), OperationKey(job), ct)));

        client.CreateJobWorker(Config("send-notification"), async (job, ct) =>
            await InScope(services =>
            {
                var headers = job.GetCustomHeaders<WorkerHeaders>();
                return services.GetRequiredService<OutcomeWorkerHandlers>().SendNotificationAsync(
                    Required(job.GetVariables<ApplicationWorkerInput>()),
                    headers?.Template ?? "application-update",
                    OperationKey(job),
                    ct);
            }));

        client.CreateJobWorker(Config("escalate-review"), async (job, ct) =>
            await InScope(services => services.GetRequiredService<OutcomeWorkerHandlers>().SendNotificationAsync(
                Required(job.GetVariables<ApplicationWorkerInput>()),
                "manual-review-sla",
                OperationKey(job),
                ct)));

        client.CreateJobWorker(Config("complete-application"), async (job, ct) =>
        {
            await InScope(async services =>
            {
                var input = Required(job.GetVariables<CompletionWorkerInput>());
                var headers = job.GetCustomHeaders<WorkerHeaders>();
                var outcome = OutcomeWorkerHandlers.ResolveOutcome(
                    headers?.Outcome,
                    input.RiskDecision?.Outcome,
                    input.ManualDecision);
                var reason = input.RiskDecision?.ReasonCode
                    ?? (outcome == "WITHDRAWN" ? "APPLICANT_REQUEST" : "MANUAL_DECISION");
                await services.GetRequiredService<OutcomeWorkerHandlers>()
                    .CompleteAsync(new(input.ApplicationId), outcome, reason, ct);
                return true;
            });
        });
    }

    private async Task<T> RunCheck<T>(
        ActivatedJob job,
        string checkType,
        Func<CheckWorkerHandlers, WorkerApplicationInput, Task<T>> action,
        CancellationToken cancellationToken)
    {
        return await InScope(async services =>
        {
            var input = Required(job.GetVariables<WorkerApplicationInput>());
            var output = await action(services.GetRequiredService<CheckWorkerHandlers>(), input);
            await services.GetRequiredService<WorkerStateService>().RecordCheckAsync(
                input.Id,
                checkType,
                JsonSerializer.Serialize(output, JsonOptions),
                OperationKey(job),
                cancellationToken);
            return output;
        });
    }

    private async Task<T> InScope<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    private static JobWorkerConfig Config(string jobType) => new()
    {
        JobType = jobType,
        JobTimeoutMs = 30_000,
        MaxConcurrentJobs = 8,
        WorkerName = $"angleflow-{jobType}"
    };

    private static T Required<T>(T? value) where T : class =>
        value ?? throw new InvalidOperationException("Camunda job variables did not match the worker contract.");

    private static string OperationKey(ActivatedJob job) => $"{job.Type}:{job.JobKey}";
}
