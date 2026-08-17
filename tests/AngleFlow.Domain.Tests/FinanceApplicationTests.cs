using AngleFlow.Domain.Applications;

namespace AngleFlow.Domain.Tests;

public sealed class FinanceApplicationTests
{
    private static readonly CreateApplicationCommand Valid = new(
        "Taylor Morgan",
        "taylor@example.test",
        120_000m,
        3_500m,
        "vehicle",
        65_000m,
        45_000m,
        48,
        "normal");

    [Fact]
    public void Create_starts_submitted_with_audit_evidence()
    {
        var application = FinanceApplication.Create(Valid, new DateTimeOffset(2026, 8, 17, 1, 0, 0, TimeSpan.Zero));

        Assert.NotEqual(Guid.Empty, application.Id.Value);
        Assert.Equal(ApplicationStatus.Submitted, application.Status);
        var audit = Assert.Single(application.AuditEvents);
        Assert.Equal("APPLICATION_SUBMITTED", audit.EventType);
        Assert.Equal(ApplicationStatus.Submitted, audit.Status);
    }

    [Fact]
    public void Transition_records_ordered_status_history()
    {
        var application = FinanceApplication.Create(Valid, DateTimeOffset.UnixEpoch);

        application.TransitionTo(ApplicationStatus.ChecksRunning, "CHECKS_STARTED", DateTimeOffset.UnixEpoch.AddMinutes(1));
        application.TransitionTo(ApplicationStatus.ManualReview, "POLICY_REVIEW", DateTimeOffset.UnixEpoch.AddMinutes(2));

        Assert.Equal(ApplicationStatus.ManualReview, application.Status);
        Assert.Equal(
            new[] { "APPLICATION_SUBMITTED", "CHECKS_STARTED", "MANUAL_REVIEW_REQUIRED" },
            application.AuditEvents.Select(item => item.EventType));
        Assert.Equal("POLICY_REVIEW", application.AuditEvents[^1].ReasonCode);
    }

    [Fact]
    public void Transition_rejects_changes_after_terminal_status()
    {
        var application = FinanceApplication.Create(Valid, DateTimeOffset.UnixEpoch);
        application.TransitionTo(ApplicationStatus.ChecksRunning, "CHECKS_STARTED", DateTimeOffset.UnixEpoch.AddMinutes(1));
        application.TransitionTo(ApplicationStatus.Approved, "LOW_RISK", DateTimeOffset.UnixEpoch.AddMinutes(2));

        var exception = Assert.Throws<InvalidApplicationTransitionException>(
            () => application.TransitionTo(
                ApplicationStatus.Withdrawn,
                "WITHDRAWN",
                DateTimeOffset.UnixEpoch.AddMinutes(3)));

        Assert.Equal("INVALID_STATUS_TRANSITION", exception.Code);
        Assert.Equal(ApplicationStatus.Approved, application.Status);
    }
}
