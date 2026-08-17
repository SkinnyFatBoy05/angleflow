namespace AngleFlow.Domain.Applications;

public sealed record CreateApplicationCommand(
    string ApplicantName,
    string Email,
    decimal AnnualIncome,
    decimal MonthlyExpenses,
    string AssetType,
    decimal AssetValue,
    decimal RequestedAmount,
    int TermMonths,
    string Scenario);

public sealed record ValidationError(string Code, string Field, string Message);

public sealed record CheckBundle(
    bool IdentityVerified,
    CreditBand CreditBand,
    decimal AffordabilityRatio,
    int FraudScore,
    decimal AssetConfidence);

public sealed record RiskDecision(
    RiskOutcome Outcome,
    string ReasonCode,
    ReviewPriority Priority);
