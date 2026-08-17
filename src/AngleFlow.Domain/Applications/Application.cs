using AngleFlow.Domain.Audit;
using AngleFlow.Domain.Common;

namespace AngleFlow.Domain.Applications;

public readonly record struct ApplicationId(Guid Value)
{
    public static ApplicationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public sealed class ApplicationValidationException(IReadOnlyList<ValidationError> errors)
    : DomainException("APPLICATION_INVALID", "Application validation failed.")
{
    public IReadOnlyList<ValidationError> Errors { get; } = errors;
}

public sealed class InvalidApplicationTransitionException(ApplicationStatus from, ApplicationStatus to)
    : DomainException("INVALID_STATUS_TRANSITION", $"Cannot transition an application from {from} to {to}.");

public sealed class FinanceApplication
{
    private static readonly IReadOnlyDictionary<ApplicationStatus, HashSet<ApplicationStatus>> AllowedTransitions =
        new Dictionary<ApplicationStatus, HashSet<ApplicationStatus>>
        {
            [ApplicationStatus.Submitted] = [ApplicationStatus.ChecksRunning, ApplicationStatus.Withdrawn, ApplicationStatus.StartFailed],
            [ApplicationStatus.StartFailed] = [ApplicationStatus.ChecksRunning, ApplicationStatus.Withdrawn],
            [ApplicationStatus.ChecksRunning] = [ApplicationStatus.ManualReview, ApplicationStatus.Approved, ApplicationStatus.Declined, ApplicationStatus.Withdrawn],
            [ApplicationStatus.ManualReview] = [ApplicationStatus.Approved, ApplicationStatus.Declined, ApplicationStatus.Withdrawn]
        };

    private readonly List<AuditEvent> _auditEvents = [];

    private FinanceApplication(ApplicationId id, CreateApplicationCommand command, DateTimeOffset createdAt)
    {
        Id = id;
        Command = command;
        Status = ApplicationStatus.Submitted;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        AddAudit("APPLICATION_SUBMITTED", "SUBMITTED", createdAt);
    }

    public ApplicationId Id { get; }

    public CreateApplicationCommand Command { get; }

    public ApplicationStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<AuditEvent> AuditEvents => _auditEvents;

    public static FinanceApplication Create(CreateApplicationCommand command, DateTimeOffset createdAt)
    {
        var errors = ApplicationValidator.Validate(command);
        if (errors.Count > 0)
        {
            throw new ApplicationValidationException(errors);
        }

        return new FinanceApplication(ApplicationId.New(), command, createdAt);
    }

    public void TransitionTo(ApplicationStatus next, string reasonCode, DateTimeOffset occurredAt)
    {
        if (!AllowedTransitions.TryGetValue(Status, out var allowed) || !allowed.Contains(next))
        {
            throw new InvalidApplicationTransitionException(Status, next);
        }

        Status = next;
        UpdatedAt = occurredAt;
        AddAudit(EventTypeFor(next), reasonCode, occurredAt);
    }

    private void AddAudit(string eventType, string reasonCode, DateTimeOffset occurredAt) =>
        _auditEvents.Add(new(_auditEvents.Count + 1, eventType, Status, reasonCode, occurredAt));

    private static string EventTypeFor(ApplicationStatus status) => status switch
    {
        ApplicationStatus.ChecksRunning => "CHECKS_STARTED",
        ApplicationStatus.ManualReview => "MANUAL_REVIEW_REQUIRED",
        ApplicationStatus.Approved => "APPLICATION_APPROVED",
        ApplicationStatus.Declined => "APPLICATION_DECLINED",
        ApplicationStatus.Withdrawn => "APPLICATION_WITHDRAWN",
        ApplicationStatus.StartFailed => "WORKFLOW_START_FAILED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported status transition.")
    };
}
