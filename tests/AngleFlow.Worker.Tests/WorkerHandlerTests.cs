using AngleFlow.Infrastructure.Checks;
using AngleFlow.Worker.Workers;
using System.Text.Json;
using DomainApplicationId = AngleFlow.Domain.Applications.ApplicationId;

namespace AngleFlow.Worker.Tests;

public sealed class WorkerHandlerTests
{
    [Fact]
    public async Task Identity_handler_returns_only_identity_contract()
    {
        var handlers = new CheckWorkerHandlers(new DeterministicCheckEngine());
        var input = new WorkerApplicationInput(
            new DomainApplicationId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")).ToString(),
            "auto-approve",
            120_000m,
            3_500m,
            65_000m,
            45_000m);

        var output = await handlers.IdentityAsync(input, CancellationToken.None);

        Assert.True(output.IdentityVerified);
    }

    [Fact]
    public async Task Check_handlers_expose_the_values_consumed_by_dmn()
    {
        var handlers = new CheckWorkerHandlers(new DeterministicCheckEngine());
        var input = new WorkerApplicationInput(
            new DomainApplicationId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")).ToString(),
            "auto-approve",
            120_000m,
            3_500m,
            65_000m,
            45_000m);

        Assert.True((await handlers.ValidateAsync(input, CancellationToken.None)).Validated);
        Assert.Equal("GOOD", (await handlers.CreditAsync(input, CancellationToken.None)).CreditBand);
        Assert.Equal(0.28m, (await handlers.AffordabilityAsync(input, CancellationToken.None)).AffordabilityRatio);
        Assert.Equal(8, (await handlers.FraudAsync(input, CancellationToken.None)).FraudScore);
        Assert.Equal(0.93m, (await handlers.AssetAsync(input, CancellationToken.None)).AssetConfidence);
    }

    [Fact]
    public void Definitions_cover_every_executable_job_type()
    {
        var expected = new[]
        {
            "validate-application", "identity-check", "credit-check",
            "affordability-check", "fraud-check", "asset-check",
            "persist-decision", "create-offer", "send-notification",
            "escalate-review", "complete-application"
        };

        Assert.Equal(expected.Order(), WorkerDefinitions.JobTypes.Order());
    }

    [Fact]
    public void Definitions_include_every_deployable_workflow_resource()
    {
        Assert.Equal(
            ["asset-finance-application.bpmn", "application-risk.dmn", "manual-review.form"],
            WorkerDefinitions.ResourceFiles);
    }

    [Fact]
    public void Worker_input_deserializes_the_string_id_published_to_camunda()
    {
        var input = JsonSerializer.Deserialize<WorkerApplicationInput>("""
            {
              "applicationId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
              "scenario": "auto-approve",
              "annualIncome": 120000,
              "monthlyExpenses": 3500,
              "assetValue": 65000,
              "requestedAmount": 45000
            }
            """, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(input);
        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), input.Id.Value);
    }
}
