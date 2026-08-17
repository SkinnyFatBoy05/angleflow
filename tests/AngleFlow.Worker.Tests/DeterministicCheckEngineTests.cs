using AngleFlow.Domain.Applications;
using AngleFlow.Infrastructure.Checks;
using DomainApplicationId = AngleFlow.Domain.Applications.ApplicationId;

namespace AngleFlow.Worker.Tests;

public sealed class DeterministicCheckEngineTests
{
    private static readonly DomainApplicationId Id = new(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));

    [Theory]
    [InlineData("auto-approve", true, CreditBand.Good, 0.28, 8, 0.93)]
    [InlineData("manual-review", true, CreditBand.Fair, 0.37, 18, 0.82)]
    [InlineData("decline", false, CreditBand.Poor, 0.52, 78, 0.55)]
    public async Task Named_scenarios_return_fixed_explainable_bundle(
        string scenario,
        bool identity,
        CreditBand credit,
        double affordability,
        int fraud,
        double assetConfidence)
    {
        var engine = new DeterministicCheckEngine();

        var result = await engine.RunAllAsync(Snapshot(scenario), CancellationToken.None);

        Assert.Equal(identity, result.IdentityVerified);
        Assert.Equal(credit, result.CreditBand);
        Assert.Equal((decimal)affordability, result.AffordabilityRatio);
        Assert.Equal(fraud, result.FraudScore);
        Assert.Equal((decimal)assetConfidence, result.AssetConfidence);
    }

    [Fact]
    public async Task Transient_failure_succeeds_on_third_attempt()
    {
        var engine = new DeterministicCheckEngine();
        var snapshot = Snapshot("transient-failure");

        await Assert.ThrowsAsync<TransientCheckException>(() => engine.RunAsync(CheckType.Credit, snapshot, CancellationToken.None));
        await Assert.ThrowsAsync<TransientCheckException>(() => engine.RunAsync(CheckType.Credit, snapshot, CancellationToken.None));
        var result = await engine.RunAsync(CheckType.Credit, snapshot, CancellationToken.None);

        Assert.Equal(CreditBand.Good, result.CreditBand);
    }

    [Fact]
    public async Task Incident_scenario_remains_retryable_for_operate_visibility()
    {
        var engine = new DeterministicCheckEngine();

        var exception = await Assert.ThrowsAsync<TransientCheckException>(
            () => engine.RunAsync(CheckType.Fraud, Snapshot("incident"), CancellationToken.None));

        Assert.Equal("SYNTHETIC_PROVIDER_UNAVAILABLE", exception.Code);
    }

    [Fact]
    public async Task Normal_scenario_is_stable_for_same_application_id()
    {
        var first = new DeterministicCheckEngine();
        var second = new DeterministicCheckEngine();

        var firstResult = await first.RunAllAsync(Snapshot("normal"), CancellationToken.None);
        var secondResult = await second.RunAllAsync(Snapshot("normal"), CancellationToken.None);

        Assert.Equal(firstResult, secondResult);
    }

    private static ApplicationSnapshot Snapshot(string scenario) => new(
        Id,
        scenario,
        120_000m,
        3_500m,
        65_000m,
        45_000m);
}
