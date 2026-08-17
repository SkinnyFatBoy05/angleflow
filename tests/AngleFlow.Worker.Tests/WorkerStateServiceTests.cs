using AngleFlow.Domain.Applications;
using AngleFlow.Infrastructure.Persistence;
using AngleFlow.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using DomainApplicationId = AngleFlow.Domain.Applications.ApplicationId;

namespace AngleFlow.Worker.Tests;

public sealed class WorkerStateServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private AngleFlowDbContext _db = null!;
    private WorkerStateService _service = null!;
    private static readonly Guid RecordId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly DomainApplicationId Id = new(RecordId);

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AngleFlowDbContext>().UseSqlite(_connection).Options;
        _db = new AngleFlowDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _db.Applications.Add(NewApplication());
        await _db.SaveChangesAsync();
        _service = new WorkerStateService(_db, TimeProvider.System);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Persist_decision_moves_medium_risk_to_manual_review()
    {
        await _service.PersistDecisionAsync(Id, RiskOutcome.ManualReview, "POLICY_REVIEW", CancellationToken.None);

        var application = await _db.Applications.SingleAsync();
        Assert.Equal(ApplicationStatus.ManualReview, application.Status);
        Assert.Equal("MANUAL_REVIEW_REQUIRED", (await _db.AuditEvents.OrderBy(item => item.Sequence).ToListAsync())[^1].EventType);
    }

    [Fact]
    public async Task Complete_auto_approval_sets_terminal_status()
    {
        await _service.CompleteAsync(Id, "AUTO_APPROVE", "LOW_RISK", CancellationToken.None);

        Assert.Equal(ApplicationStatus.Approved, (await _db.Applications.SingleAsync()).Status);
    }

    [Fact]
    public async Task Complete_is_idempotent_when_the_terminal_status_is_already_recorded()
    {
        await _service.CompleteAsync(Id, "WITHDRAWN", "APPLICANT_REQUEST", CancellationToken.None);
        await _service.CompleteAsync(Id, "WITHDRAWN", "APPLICANT_REQUEST", CancellationToken.None);

        Assert.Equal(ApplicationStatus.Withdrawn, (await _db.Applications.SingleAsync()).Status);
        Assert.Equal(2, await _db.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task Record_check_is_idempotent_by_external_operation_key()
    {
        await _service.RecordCheckAsync(Id, "credit", "{\"creditBand\":\"GOOD\"}", "check-op-001", CancellationToken.None);
        await _service.RecordCheckAsync(Id, "credit", "{\"creditBand\":\"GOOD\"}", "check-op-001", CancellationToken.None);

        Assert.Equal(1, await _db.Checks.CountAsync());
    }

    private static ApplicationRecord NewApplication() => new()
    {
        Id = RecordId,
        ApplicantName = "Synthetic Applicant",
        Email = "synthetic@example.test",
        AnnualIncome = 120_000m,
        MonthlyExpenses = 3_500m,
        AssetType = "vehicle",
        AssetValue = 65_000m,
        RequestedAmount = 45_000m,
        TermMonths = 48,
        Scenario = "auto-approve",
        Status = ApplicationStatus.ChecksRunning,
        CreatedAt = DateTimeOffset.UnixEpoch,
        UpdatedAt = DateTimeOffset.UnixEpoch,
        Version = 1,
        AuditEvents =
        [
            new AuditRecord
            {
                ApplicationId = RecordId,
                Sequence = 1,
                EventType = "CHECKS_STARTED",
                Status = ApplicationStatus.ChecksRunning,
                ReasonCode = "WORKFLOW_STARTED",
                OccurredAt = DateTimeOffset.UnixEpoch
            }
        ]
    };
}
