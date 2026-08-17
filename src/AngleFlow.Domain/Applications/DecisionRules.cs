namespace AngleFlow.Domain.Applications;

public static class DecisionRules
{
    public static RiskDecision Evaluate(CheckBundle checks)
    {
        if (!checks.IdentityVerified)
        {
            return new(RiskOutcome.Decline, "IDENTITY_FAILED", ReviewPriority.None);
        }

        if (checks.FraudScore >= 70)
        {
            return new(RiskOutcome.Decline, "FRAUD_RISK", ReviewPriority.None);
        }

        if (checks.CreditBand == CreditBand.Poor)
        {
            return new(RiskOutcome.Decline, "CREDIT_RISK", ReviewPriority.None);
        }

        if (checks.AffordabilityRatio > 0.45m)
        {
            return new(RiskOutcome.Decline, "AFFORDABILITY", ReviewPriority.None);
        }

        if (checks.CreditBand == CreditBand.Fair
            || checks.AffordabilityRatio >= 0.35m
            || checks.FraudScore >= 30
            || checks.AssetConfidence < 0.75m)
        {
            var priority = checks.FraudScore >= 50 || checks.AssetConfidence < 0.60m
                ? ReviewPriority.High
                : ReviewPriority.Standard;
            return new(RiskOutcome.ManualReview, "POLICY_REVIEW", priority);
        }

        return new(RiskOutcome.AutoApprove, "LOW_RISK", ReviewPriority.None);
    }
}
