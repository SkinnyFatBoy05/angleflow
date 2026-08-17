using AngleFlow.Domain.Applications;

namespace AngleFlow.Domain.Tests;

public sealed class DecisionRulesTests
{
    [Fact]
    public void Evaluate_auto_approves_a_clean_application()
    {
        var checks = new CheckBundle(true, CreditBand.Good, 0.28m, 8, 0.93m);

        var decision = DecisionRules.Evaluate(checks);

        Assert.Equal(RiskOutcome.AutoApprove, decision.Outcome);
        Assert.Equal("LOW_RISK", decision.ReasonCode);
        Assert.Equal(ReviewPriority.None, decision.Priority);
    }

    [Fact]
    public void Evaluate_routes_medium_risk_to_manual_review()
    {
        var checks = new CheckBundle(true, CreditBand.Fair, 0.37m, 18, 0.82m);

        var decision = DecisionRules.Evaluate(checks);

        Assert.Equal(RiskOutcome.ManualReview, decision.Outcome);
        Assert.Equal("POLICY_REVIEW", decision.ReasonCode);
        Assert.Equal(ReviewPriority.Standard, decision.Priority);
    }

    [Fact]
    public void Evaluate_declines_when_identity_is_not_verified()
    {
        var checks = new CheckBundle(false, CreditBand.Good, 0.20m, 2, 0.96m);

        var decision = DecisionRules.Evaluate(checks);

        Assert.Equal(RiskOutcome.Decline, decision.Outcome);
        Assert.Equal("IDENTITY_FAILED", decision.ReasonCode);
        Assert.Equal(ReviewPriority.None, decision.Priority);
    }

    [Fact]
    public void Evaluate_declines_high_fraud_risk_before_other_policy_rules()
    {
        var checks = new CheckBundle(true, CreditBand.Fair, 0.38m, 78, 0.81m);

        var decision = DecisionRules.Evaluate(checks);

        Assert.Equal(RiskOutcome.Decline, decision.Outcome);
        Assert.Equal("FRAUD_RISK", decision.ReasonCode);
    }
}
