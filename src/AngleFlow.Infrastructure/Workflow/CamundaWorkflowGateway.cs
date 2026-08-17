using AngleFlow.Domain.Applications;
using Camunda.Orchestration.Sdk;
using DomainApplicationId = AngleFlow.Domain.Applications.ApplicationId;

namespace AngleFlow.Infrastructure.Workflow;

public sealed record StartProcessRequest(
    string ProcessDefinitionId,
    IReadOnlyDictionary<string, object?> Variables);

public sealed record PublishWorkflowMessageRequest(
    string Name,
    string CorrelationKey,
    string MessageId,
    IReadOnlyDictionary<string, object?> Variables);

public interface ICamundaOperationsClient
{
    Task<string> StartProcessAsync(StartProcessRequest request, CancellationToken cancellationToken);
    Task PublishMessageAsync(PublishWorkflowMessageRequest request, CancellationToken cancellationToken);
}

public sealed class CamundaWorkflowGateway(ICamundaOperationsClient client) : IWorkflowGateway
{
    public Task<string> StartApplicationAsync(
        DomainApplicationId id,
        CreateApplicationCommand command,
        CancellationToken cancellationToken)
    {
        var variables = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["applicationId"] = id.ToString(),
            ["scenario"] = command.Scenario.Trim().ToLowerInvariant(),
            ["annualIncome"] = command.AnnualIncome,
            ["monthlyExpenses"] = command.MonthlyExpenses,
            ["assetType"] = command.AssetType.Trim().ToLowerInvariant(),
            ["assetValue"] = command.AssetValue,
            ["requestedAmount"] = command.RequestedAmount,
            ["termMonths"] = command.TermMonths
        };

        return client.StartProcessAsync(
            new("asset-finance-application", variables),
            cancellationToken);
    }

    public Task CorrelateWithdrawalAsync(DomainApplicationId id, CancellationToken cancellationToken) =>
        client.PublishMessageAsync(
            new(
                "application-withdrawn",
                id.ToString(),
                $"withdraw-{id}",
                new Dictionary<string, object?> { ["withdrawalRequested"] = true }),
            cancellationToken);
}

public sealed class SdkCamundaOperationsClient(CamundaClient client) : ICamundaOperationsClient
{
    public async Task<string> StartProcessAsync(StartProcessRequest request, CancellationToken cancellationToken)
    {
        var result = await client.CreateProcessInstanceAsync(
            new ProcessInstanceCreationInstructionById
            {
                ProcessDefinitionId = ProcessDefinitionId.AssumeExists(request.ProcessDefinitionId),
                Variables = request.Variables
            },
            cancellationToken);
        return result.ProcessInstanceKey.ToString();
    }

    public async Task PublishMessageAsync(
        PublishWorkflowMessageRequest request,
        CancellationToken cancellationToken)
    {
        await client.PublishMessageAsync(
            new MessagePublicationRequest
            {
                Name = request.Name,
                CorrelationKey = request.CorrelationKey,
                MessageId = request.MessageId,
                TimeToLive = 60_000,
                Variables = request.Variables
            },
            cancellationToken);
    }
}
