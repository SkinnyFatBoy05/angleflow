using AngleFlow.Domain.Applications;
using AngleFlow.Domain.Common;
using AngleFlow.Infrastructure.Persistence;
using AngleFlow.Infrastructure.Workflow;
using Microsoft.EntityFrameworkCore;
using DomainApplicationId = AngleFlow.Domain.Applications.ApplicationId;

namespace AngleFlow.Infrastructure.Services;

public sealed record ApplicationView(
    DomainApplicationId Id,
    ApplicationStatus Status,
    string AssetType,
    decimal AssetValue,
    decimal RequestedAmount,
    int TermMonths,
    string Scenario,
    string? ProcessInstanceKey,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record TimelineEventView(
    long Sequence,
    string EventType,
    ApplicationStatus Status,
    string ReasonCode,
    DateTimeOffset OccurredAt);

public sealed record CreateApplicationResult(ApplicationView Application, bool Replayed);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public sealed class IdempotencyConflictException()
    : DomainException("IDEMPOTENCY_KEY_REUSED", "The idempotency key was already used with a different request.");

public sealed class InvalidIdempotencyKeyException()
    : DomainException("IDEMPOTENCY_KEY_INVALID", "Idempotency-Key must contain between 1 and 128 characters.");

public sealed class WorkflowStartException(DomainApplicationId applicationId, Exception innerException)
    : Exception("The application was stored but its workflow could not be started.", innerException)
{
    public DomainApplicationId ApplicationId { get; } = applicationId;
}

public sealed class ApplicationNotFoundException(DomainApplicationId applicationId)
    : DomainException("APPLICATION_NOT_FOUND", $"Application {applicationId} was not found.");

public interface IApplicationService
{
    Task<CreateApplicationResult> CreateAsync(CreateApplicationCommand command, string idempotencyKey, CancellationToken cancellationToken);
    Task<ApplicationView?> GetAsync(DomainApplicationId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<TimelineEventView>> GetTimelineAsync(DomainApplicationId id, CancellationToken cancellationToken);
    Task<PagedResult<ApplicationView>> ListAsync(int page, int pageSize, ApplicationStatus? status, CancellationToken cancellationToken);
    Task<ApplicationView> WithdrawAsync(DomainApplicationId id, CancellationToken cancellationToken);
}

public sealed class ApplicationService(
    AngleFlowDbContext db,
    IWorkflowGateway workflow,
    TimeProvider timeProvider) : IApplicationService
{
    public async Task<CreateApplicationResult> CreateAsync(
        CreateApplicationCommand command,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var validationErrors = ApplicationValidator.Validate(command);
        if (validationErrors.Count > 0)
        {
            throw new ApplicationValidationException(validationErrors);
        }

        idempotencyKey = idempotencyKey.Trim();
        if (idempotencyKey.Length is < 1 or > 128)
        {
            throw new InvalidIdempotencyKeyException();
        }

        var payloadHash = HashingService.Hash(command);
        var existingKey = await db.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Key == idempotencyKey, cancellationToken);
        if (existingKey is not null)
        {
            if (!string.Equals(existingKey.PayloadHash, payloadHash, StringComparison.Ordinal))
            {
                throw new IdempotencyConflictException();
            }

            var replayed = await GetRequiredAsync(new(existingKey.ApplicationId), cancellationToken);
            return new(replayed, true);
        }

        var now = timeProvider.GetUtcNow();
        var record = ToRecord(command, now);
        record.AuditEvents.Add(NewAudit(record, 1, "APPLICATION_SUBMITTED", "SUBMITTED", now));
        db.Applications.Add(record);
        db.IdempotencyRecords.Add(new()
        {
            Key = idempotencyKey,
            PayloadHash = payloadHash,
            ApplicationId = record.Id,
            CreatedAt = now
        });
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            record.ProcessInstanceKey = await workflow.StartApplicationAsync(new(record.Id), command, cancellationToken);
            Transition(record, ApplicationStatus.ChecksRunning, "CHECKS_STARTED", "WORKFLOW_STARTED", timeProvider.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
            return new(ToView(record), false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Transition(record, ApplicationStatus.StartFailed, "WORKFLOW_START_FAILED", "CAMUNDA_UNAVAILABLE", timeProvider.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
            throw new WorkflowStartException(new(record.Id), exception);
        }
    }

    public async Task<ApplicationView?> GetAsync(DomainApplicationId id, CancellationToken cancellationToken)
    {
        var record = await db.Applications.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken);
        return record is null ? null : ToView(record);
    }

    public async Task<IReadOnlyList<TimelineEventView>> GetTimelineAsync(DomainApplicationId id, CancellationToken cancellationToken) =>
        await db.AuditEvents
            .AsNoTracking()
            .Where(item => item.ApplicationId == id.Value)
            .OrderBy(item => item.Sequence)
            .Select(item => new TimelineEventView(item.Sequence, item.EventType, item.Status, item.ReasonCode, item.OccurredAt))
            .ToListAsync(cancellationToken);

    public async Task<PagedResult<ApplicationView>> ListAsync(
        int page,
        int pageSize,
        ApplicationStatus? status,
        CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Applications.AsNoTracking();
        if (status is not null)
        {
            query = query.Where(item => item.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var records = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new(records.Select(ToView).ToArray(), page, pageSize, total);
    }

    public async Task<ApplicationView> WithdrawAsync(DomainApplicationId id, CancellationToken cancellationToken)
    {
        var record = await db.Applications
            .Include(item => item.AuditEvents)
            .SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken)
            ?? throw new ApplicationNotFoundException(id);
        if (record.Status is ApplicationStatus.Approved or ApplicationStatus.Declined or ApplicationStatus.Withdrawn)
        {
            throw new InvalidApplicationTransitionException(record.Status, ApplicationStatus.Withdrawn);
        }

        await workflow.CorrelateWithdrawalAsync(id, cancellationToken);
        await db.Entry(record).ReloadAsync(cancellationToken);
        db.Entry(record).Collection(item => item.AuditEvents).IsLoaded = false;
        await db.Entry(record).Collection(item => item.AuditEvents).LoadAsync(cancellationToken);
        if (record.Status == ApplicationStatus.Withdrawn)
        {
            return ToView(record);
        }

        Transition(record, ApplicationStatus.Withdrawn, "APPLICATION_WITHDRAWN", "APPLICANT_REQUEST", timeProvider.GetUtcNow());
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return ToView(record);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var completedByWorker = await db.Applications.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken);
            if (completedByWorker?.Status == ApplicationStatus.Withdrawn)
            {
                return ToView(completedByWorker);
            }

            throw;
        }
    }

    private async Task<ApplicationView> GetRequiredAsync(DomainApplicationId id, CancellationToken cancellationToken) =>
        await GetAsync(id, cancellationToken) ?? throw new ApplicationNotFoundException(id);

    private static ApplicationRecord ToRecord(CreateApplicationCommand command, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        ApplicantName = command.ApplicantName.Trim(),
        Email = command.Email.Trim().ToLowerInvariant(),
        AnnualIncome = command.AnnualIncome,
        MonthlyExpenses = command.MonthlyExpenses,
        AssetType = command.AssetType.Trim().ToLowerInvariant(),
        AssetValue = command.AssetValue,
        RequestedAmount = command.RequestedAmount,
        TermMonths = command.TermMonths,
        Scenario = command.Scenario.Trim().ToLowerInvariant(),
        Status = ApplicationStatus.Submitted,
        CreatedAt = now,
        UpdatedAt = now,
        Version = 1
    };

    private static void Transition(
        ApplicationRecord record,
        ApplicationStatus status,
        string eventType,
        string reasonCode,
        DateTimeOffset occurredAt)
    {
        record.Status = status;
        record.UpdatedAt = occurredAt;
        record.Version++;
        var nextSequence = record.AuditEvents.Count == 0
            ? 1
            : record.AuditEvents.Max(item => item.Sequence) + 1;
        record.AuditEvents.Add(NewAudit(record, nextSequence, eventType, reasonCode, occurredAt));
    }

    private static AuditRecord NewAudit(
        ApplicationRecord record,
        long sequence,
        string eventType,
        string reasonCode,
        DateTimeOffset occurredAt) => new()
    {
        ApplicationId = record.Id,
        Sequence = sequence,
        EventType = eventType,
        Status = record.Status,
        ReasonCode = reasonCode,
        OccurredAt = occurredAt
    };

    private static ApplicationView ToView(ApplicationRecord record) => new(
        new(record.Id),
        record.Status,
        record.AssetType,
        record.AssetValue,
        record.RequestedAmount,
        record.TermMonths,
        record.Scenario,
        record.ProcessInstanceKey,
        record.CreatedAt,
        record.UpdatedAt);
}
