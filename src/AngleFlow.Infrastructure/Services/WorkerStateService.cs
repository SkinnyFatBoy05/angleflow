using AngleFlow.Domain.Applications;
using AngleFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainApplicationId = AngleFlow.Domain.Applications.ApplicationId;

namespace AngleFlow.Infrastructure.Services;

public sealed class WorkerStateService(AngleFlowDbContext db, TimeProvider timeProvider)
{
    public async Task RecordCheckAsync(
        DomainApplicationId applicationId,
        string checkType,
        string outcomeJson,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (await db.Checks.AsNoTracking().AnyAsync(item => item.ExternalOperationKey == operationKey, cancellationToken))
        {
            return;
        }

        db.Checks.Add(new()
        {
            ApplicationId = applicationId.Value,
            CheckType = checkType,
            OutcomeJson = outcomeJson,
            ExternalOperationKey = operationKey,
            CompletedAt = timeProvider.GetUtcNow()
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task PersistDecisionAsync(
        DomainApplicationId applicationId,
        RiskOutcome outcome,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        var record = await RequiredAsync(applicationId, cancellationToken);
        var status = outcome switch
        {
            RiskOutcome.ManualReview => ApplicationStatus.ManualReview,
            RiskOutcome.Decline => ApplicationStatus.Declined,
            _ => ApplicationStatus.ChecksRunning
        };
        var eventType = outcome switch
        {
            RiskOutcome.ManualReview => "MANUAL_REVIEW_REQUIRED",
            RiskOutcome.Decline => "APPLICATION_DECLINED",
            _ => "AUTO_APPROVAL_RECORDED"
        };
        Transition(record, status, eventType, reasonCode);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteAsync(
        DomainApplicationId applicationId,
        string outcome,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        var record = await RequiredAsync(applicationId, cancellationToken);
        var status = outcome.Trim().ToUpperInvariant() switch
        {
            "AUTO_APPROVE" or "APPROVE" => ApplicationStatus.Approved,
            "DECLINE" => ApplicationStatus.Declined,
            "WITHDRAWN" => ApplicationStatus.Withdrawn,
            _ => throw new ArgumentException("Unsupported completion outcome.", nameof(outcome))
        };
        var eventType = status switch
        {
            ApplicationStatus.Approved => "APPLICATION_APPROVED",
            ApplicationStatus.Declined => "APPLICATION_DECLINED",
            _ => "APPLICATION_WITHDRAWN"
        };
        Transition(record, status, eventType, reasonCode);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<ApplicationRecord> RequiredAsync(DomainApplicationId applicationId, CancellationToken cancellationToken) =>
        await db.Applications.Include(item => item.AuditEvents)
            .SingleOrDefaultAsync(item => item.Id == applicationId.Value, cancellationToken)
        ?? throw new ApplicationNotFoundException(applicationId);

    private void Transition(ApplicationRecord record, ApplicationStatus status, string eventType, string reasonCode)
    {
        var now = timeProvider.GetUtcNow();
        record.Status = status;
        record.UpdatedAt = now;
        record.Version++;
        record.AuditEvents.Add(new()
        {
            ApplicationId = record.Id,
            Sequence = record.AuditEvents.Count == 0 ? 1 : record.AuditEvents.Max(item => item.Sequence) + 1,
            EventType = eventType,
            Status = status,
            ReasonCode = reasonCode,
            OccurredAt = now
        });
    }
}
