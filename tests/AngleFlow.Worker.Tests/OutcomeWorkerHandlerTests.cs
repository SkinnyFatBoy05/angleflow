using AngleFlow.Domain.Applications;
using AngleFlow.Infrastructure.Persistence;
using AngleFlow.Infrastructure.Services;
using AngleFlow.Worker.Workers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AngleFlow.Worker.Tests;

public sealed class OutcomeWorkerHandlerTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private AngleFlowDbContext _db = null!;
    private OutcomeWorkerHandlers _handlers = null!;
    private static readonly Guid RecordId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AngleFlowDbContext>().UseSqlite(_connection).Options;
        _db = new AngleFlowDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _db.Applications.Add(NewApplication());
        await _db.SaveChangesAsync();
        _handlers = new OutcomeWorkerHandlers(
            new WorkerStateService(_db, TimeProvider.System),
            new ExternalOperationService(_db, TimeProvider.System));
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Decision_handler_persists_the_dmn_outcome()
    {
        var input = new DecisionWorkerInput(
            RecordId.ToString("D"),
            new RiskDecisionVariables("MANUAL_REVIEW", "POLICY_REVIEW", "STANDARD"));

        await _handlers.PersistDecisionAsync(input, CancellationToken.None);

        Assert.Equal(ApplicationStatus.ManualReview, (await _db.Applications.SingleAsync()).Status);
    }

    [Fact]
    public async Task Offer_and_notification_handlers_are_idempotent_by_job_operation()
    {
        var offer = await _handlers.CreateOfferAsync(
            new ApplicationWorkerInput(RecordId.ToString("D")), "offer-job-1", CancellationToken.None);
        await _handlers.SendNotificationAsync(
            new ApplicationWorkerInput(RecordId.ToString("D")), "approved", "notify-job-1", CancellationToken.None);
        await _handlers.SendNotificationAsync(
            new ApplicationWorkerInput(RecordId.ToString("D")), "approved", "notify-job-1", CancellationToken.None);

        Assert.StartsWith("AF-", offer.OfferNumber);
        Assert.Equal(1, await _db.Offers.CountAsync());
        Assert.Equal(1, await _db.Notifications.CountAsync());
    }

    [Fact]
    public void Completion_handler_resolves_auto_and_manual_outcomes()
    {
        Assert.Equal("AUTO_APPROVE", OutcomeWorkerHandlers.ResolveOutcome(null, "AUTO_APPROVE", null));
        Assert.Equal("APPROVE", OutcomeWorkerHandlers.ResolveOutcome(null, "MANUAL_REVIEW", "APPROVE"));
        Assert.Equal("WITHDRAWN", OutcomeWorkerHandlers.ResolveOutcome("withdrawn", null, null));
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
