using AngleFlow.Domain.Applications;

namespace AngleFlow.Domain.Audit;

public sealed record AuditEvent(
    long Sequence,
    string EventType,
    ApplicationStatus Status,
    string ReasonCode,
    DateTimeOffset OccurredAt);
