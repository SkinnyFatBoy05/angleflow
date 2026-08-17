using AngleFlow.Domain.Applications;
using AngleFlow.Infrastructure.Checks;
using DomainApplicationId = AngleFlow.Domain.Applications.ApplicationId;

namespace AngleFlow.Worker.Workers;

public sealed record WorkerApplicationInput(
    string ApplicationId,
    string Scenario,
    decimal AnnualIncome,
    decimal MonthlyExpenses,
    decimal AssetValue,
    decimal RequestedAmount)
{
    public DomainApplicationId Id => new(Guid.Parse(ApplicationId));

    public ApplicationSnapshot ToSnapshot() => new(
        Id,
        Scenario,
        AnnualIncome,
        MonthlyExpenses,
        AssetValue,
        RequestedAmount);
}

public sealed record ValidationOutput(bool Validated);
public sealed record IdentityOutput(bool IdentityVerified);
public sealed record CreditOutput(string CreditBand);
public sealed record AffordabilityOutput(decimal AffordabilityRatio);
public sealed record FraudOutput(int FraudScore);
public sealed record AssetOutput(decimal AssetConfidence);

public sealed class CheckWorkerHandlers(ICheckEngine engine)
{
    public Task<ValidationOutput> ValidateAsync(WorkerApplicationInput input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ValidationOutput(true));
    }

    public async Task<IdentityOutput> IdentityAsync(WorkerApplicationInput input, CancellationToken cancellationToken) =>
        new((await engine.RunAsync(CheckType.Identity, input.ToSnapshot(), cancellationToken)).IdentityVerified);

    public async Task<CreditOutput> CreditAsync(WorkerApplicationInput input, CancellationToken cancellationToken) =>
        new((await engine.RunAsync(CheckType.Credit, input.ToSnapshot(), cancellationToken)).CreditBand
            .ToString()
            .ToUpperInvariant());

    public async Task<AffordabilityOutput> AffordabilityAsync(WorkerApplicationInput input, CancellationToken cancellationToken) =>
        new((await engine.RunAsync(CheckType.Affordability, input.ToSnapshot(), cancellationToken)).AffordabilityRatio);

    public async Task<FraudOutput> FraudAsync(WorkerApplicationInput input, CancellationToken cancellationToken) =>
        new((await engine.RunAsync(CheckType.Fraud, input.ToSnapshot(), cancellationToken)).FraudScore);

    public async Task<AssetOutput> AssetAsync(WorkerApplicationInput input, CancellationToken cancellationToken) =>
        new((await engine.RunAsync(CheckType.Asset, input.ToSnapshot(), cancellationToken)).AssetConfidence);
}
