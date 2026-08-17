using AngleFlow.Domain.Applications;
using AngleFlow.Infrastructure.Services;
using DomainApplicationId = AngleFlow.Domain.Applications.ApplicationId;

namespace AngleFlow.Worker.Workers;

public sealed record RiskDecisionVariables(string Outcome, string ReasonCode, string ReviewPriority);
public sealed record DecisionWorkerInput(string ApplicationId, RiskDecisionVariables RiskDecision);
public sealed record ApplicationWorkerInput(string ApplicationId);
public sealed record OfferOutput(string OfferNumber);
public sealed record NotificationOutput(bool NotificationSent);

public sealed class OutcomeWorkerHandlers(
    WorkerStateService state,
    ExternalOperationService externalOperations)
{
    public Task PersistDecisionAsync(DecisionWorkerInput input, CancellationToken cancellationToken) =>
        state.PersistDecisionAsync(
            ParseId(input.ApplicationId),
            ParseRiskOutcome(input.RiskDecision.Outcome),
            input.RiskDecision.ReasonCode,
            cancellationToken);

    public async Task<OfferOutput> CreateOfferAsync(
        ApplicationWorkerInput input,
        string operationKey,
        CancellationToken cancellationToken) =>
        new(await externalOperations.CreateOfferAsync(ParseId(input.ApplicationId), operationKey, cancellationToken));

    public async Task<NotificationOutput> SendNotificationAsync(
        ApplicationWorkerInput input,
        string template,
        string operationKey,
        CancellationToken cancellationToken)
    {
        await externalOperations.SendNotificationAsync(
            ParseId(input.ApplicationId), template, operationKey, cancellationToken);
        return new(true);
    }

    public Task CompleteAsync(
        ApplicationWorkerInput input,
        string outcome,
        string reasonCode,
        CancellationToken cancellationToken) =>
        state.CompleteAsync(ParseId(input.ApplicationId), outcome, reasonCode, cancellationToken);

    public static string ResolveOutcome(string? headerOutcome, string? riskOutcome, string? manualDecision)
    {
        if (!string.IsNullOrWhiteSpace(headerOutcome))
        {
            return headerOutcome.Trim().ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(manualDecision))
        {
            return manualDecision.Trim().ToUpperInvariant();
        }

        return string.IsNullOrWhiteSpace(riskOutcome)
            ? throw new ArgumentException("A workflow outcome is required.")
            : riskOutcome.Trim().ToUpperInvariant();
    }

    public static string ResolveReasonCode(string outcome, string? riskReasonCode) =>
        string.Equals(outcome, "WITHDRAWN", StringComparison.OrdinalIgnoreCase)
            ? "APPLICANT_REQUEST"
            : riskReasonCode ?? "MANUAL_DECISION";

    private static DomainApplicationId ParseId(string value) => new(Guid.Parse(value));

    private static RiskOutcome ParseRiskOutcome(string value) => value.Trim().ToUpperInvariant() switch
    {
        "AUTO_APPROVE" => RiskOutcome.AutoApprove,
        "MANUAL_REVIEW" => RiskOutcome.ManualReview,
        "DECLINE" => RiskOutcome.Decline,
        _ => throw new ArgumentException("Unsupported risk outcome.", nameof(value))
    };
}
