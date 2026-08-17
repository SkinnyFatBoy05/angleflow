using AngleFlow.Domain.Applications;
using AngleFlow.Infrastructure.Workflow;

namespace AngleFlow.Infrastructure.Tests;

public sealed class CamundaWorkflowGatewayTests
{
    private static readonly CreateApplicationCommand Command = new(
        "Taylor Morgan",
        "taylor@example.test",
        120_000m,
        3_500m,
        "vehicle",
        65_000m,
        45_000m,
        48,
        "auto-approve");

    [Fact]
    public async Task Start_maps_domain_command_to_public_workflow_variables()
    {
        var client = new CapturingCamundaClient();
        var gateway = new CamundaWorkflowGateway(client);
        var id = new AngleFlow.Domain.Applications.ApplicationId(Guid.Parse("bd102fa1-84a3-4ff2-b42e-215d04c90726"));

        var key = await gateway.StartApplicationAsync(id, Command, CancellationToken.None);

        Assert.Equal("process-42", key);
        Assert.NotNull(client.StartRequest);
        Assert.Equal("asset-finance-application", client.StartRequest.ProcessDefinitionId);
        Assert.Equal(id.ToString(), client.StartRequest.Variables["applicationId"]);
        Assert.Equal("auto-approve", client.StartRequest.Variables["scenario"]);
        Assert.Equal(45_000m, client.StartRequest.Variables["requestedAmount"]);
    }

    [Fact]
    public async Task Withdrawal_publishes_idempotent_correlated_message()
    {
        var client = new CapturingCamundaClient();
        var gateway = new CamundaWorkflowGateway(client);
        var id = new AngleFlow.Domain.Applications.ApplicationId(Guid.Parse("bd102fa1-84a3-4ff2-b42e-215d04c90726"));

        await gateway.CorrelateWithdrawalAsync(id, CancellationToken.None);

        Assert.NotNull(client.MessageRequest);
        Assert.Equal("application-withdrawn", client.MessageRequest.Name);
        Assert.Equal(id.ToString(), client.MessageRequest.CorrelationKey);
        Assert.Equal($"withdraw-{id}", client.MessageRequest.MessageId);
    }

    private sealed class CapturingCamundaClient : ICamundaOperationsClient
    {
        public StartProcessRequest? StartRequest { get; private set; }
        public PublishWorkflowMessageRequest? MessageRequest { get; private set; }

        public Task<string> StartProcessAsync(StartProcessRequest request, CancellationToken cancellationToken)
        {
            StartRequest = request;
            return Task.FromResult("process-42");
        }

        public Task PublishMessageAsync(PublishWorkflowMessageRequest request, CancellationToken cancellationToken)
        {
            MessageRequest = request;
            return Task.CompletedTask;
        }
    }
}
