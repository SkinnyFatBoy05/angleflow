using System.Collections.Concurrent;
using AngleFlow.Domain.Applications;
using AngleFlow.Domain.Common;
using DomainApplicationId = AngleFlow.Domain.Applications.ApplicationId;

namespace AngleFlow.Infrastructure.Checks;

public enum CheckType
{
    Identity,
    Credit,
    Affordability,
    Fraud,
    Asset
}

public sealed record ApplicationSnapshot(
    DomainApplicationId Id,
    string Scenario,
    decimal AnnualIncome,
    decimal MonthlyExpenses,
    decimal AssetValue,
    decimal RequestedAmount);

public sealed class TransientCheckException(string code, string message) : DomainException(code, message);

public interface ICheckEngine
{
    Task<CheckBundle> RunAsync(CheckType checkType, ApplicationSnapshot application, CancellationToken cancellationToken);
    Task<CheckBundle> RunAllAsync(ApplicationSnapshot application, CancellationToken cancellationToken);
}

public sealed class DeterministicCheckEngine : ICheckEngine
{
    private readonly ConcurrentDictionary<string, int> _attempts = new(StringComparer.Ordinal);

    public Task<CheckBundle> RunAllAsync(ApplicationSnapshot application, CancellationToken cancellationToken) =>
        RunAsync(CheckType.Identity, application, cancellationToken);

    public Task<CheckBundle> RunAsync(
        CheckType checkType,
        ApplicationSnapshot application,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var scenario = application.Scenario.Trim().ToLowerInvariant();

        if (scenario == "incident")
        {
            throw new TransientCheckException(
                "SYNTHETIC_PROVIDER_UNAVAILABLE",
                "Synthetic provider outage requested for incident demonstration.");
        }

        if (scenario == "transient-failure")
        {
            var key = $"{application.Id}:{checkType}";
            var attempt = _attempts.AddOrUpdate(key, 1, (_, previous) => previous + 1);
            if (attempt <= 2)
            {
                throw new TransientCheckException(
                    "SYNTHETIC_PROVIDER_TIMEOUT",
                    $"Synthetic transient failure on attempt {attempt}.");
            }
        }

        return Task.FromResult(scenario switch
        {
            "auto-approve" or "transient-failure" => new CheckBundle(true, CreditBand.Good, 0.28m, 8, 0.93m),
            "manual-review" => new CheckBundle(true, CreditBand.Fair, 0.37m, 18, 0.82m),
            "decline" => new CheckBundle(false, CreditBand.Poor, 0.52m, 78, 0.55m),
            _ => FromStableId(application)
        });
    }

    private static CheckBundle FromStableId(ApplicationSnapshot application)
    {
        var bytes = application.Id.Value.ToByteArray();
        var seed = bytes.Aggregate(17, (current, value) => unchecked((current * 31) + value));
        var positive = seed & int.MaxValue;
        var credit = positive % 5 == 0 ? CreditBand.Fair : CreditBand.Good;
        var affordability = 0.25m + ((positive % 14) / 100m);
        var fraud = positive % 36;
        var confidence = 0.76m + ((positive % 20) / 100m);
        return new(true, credit, affordability, fraud, confidence);
    }
}
