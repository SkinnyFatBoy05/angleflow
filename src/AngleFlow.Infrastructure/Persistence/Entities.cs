using AngleFlow.Domain.Applications;

namespace AngleFlow.Infrastructure.Persistence;

public sealed class ApplicationRecord
{
    public Guid Id { get; set; }
    public string ApplicantName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal AnnualIncome { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public string AssetType { get; set; } = string.Empty;
    public decimal AssetValue { get; set; }
    public decimal RequestedAmount { get; set; }
    public int TermMonths { get; set; }
    public string Scenario { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; }
    public string? ProcessInstanceKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; }
    public List<AuditRecord> AuditEvents { get; set; } = [];
}

public sealed class AuditRecord
{
    public long Id { get; set; }
    public Guid ApplicationId { get; set; }
    public long Sequence { get; set; }
    public string EventType { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public ApplicationRecord Application { get; set; } = null!;
}

public sealed class IdempotencyRecord
{
    public string Key { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public Guid ApplicationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class CheckRecord
{
    public long Id { get; set; }
    public Guid ApplicationId { get; set; }
    public string CheckType { get; set; } = string.Empty;
    public string OutcomeJson { get; set; } = string.Empty;
    public string ExternalOperationKey { get; set; } = string.Empty;
    public DateTimeOffset CompletedAt { get; set; }
}

public sealed class OfferRecord
{
    public long Id { get; set; }
    public Guid ApplicationId { get; set; }
    public string OfferNumber { get; set; } = string.Empty;
    public string ExternalOperationKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class NotificationRecord
{
    public long Id { get; set; }
    public Guid ApplicationId { get; set; }
    public string Template { get; set; } = string.Empty;
    public string ExternalOperationKey { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; }
}
