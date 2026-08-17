using AngleFlow.Domain.Applications;
using AngleFlow.Infrastructure.Persistence;
using AngleFlow.Infrastructure.Services;
using AngleFlow.Infrastructure.Workflow;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using DomainApplicationId = AngleFlow.Domain.Applications.ApplicationId;

namespace AngleFlow.Infrastructure.Tests;

public sealed class ApplicationServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private AngleFlowDbContext _db = null!;
    private SingleStartWorkflowGateway _workflow = null!;
    private ApplicationService _service = null!;

    private static readonly CreateApplicationCommand Valid = new(
        "Taylor Morgan",
        "taylor@example.test",
        120_000m,
        3_500m,
        "vehicle",
        65_000m,
        45_000m,
        48,
        "auto-approve");

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AngleFlowDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AngleFlowDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _workflow = new SingleStartWorkflowGateway();
        _service = new ApplicationService(_db, _workflow, TimeProvider.System);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Create_persists_application_and_process_key()
    {
        var result = await _service.CreateAsync(Valid, "create-001", CancellationToken.None);

        Assert.False(result.Replayed);
        Assert.Equal(ApplicationStatus.ChecksRunning, result.Application.Status);
        Assert.Equal("process-001", result.Application.ProcessInstanceKey);
        Assert.Equal(2, (await _service.GetTimelineAsync(result.Application.Id, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task Create_replays_equivalent_request_without_starting_a_second_process()
    {
        var first = await _service.CreateAsync(Valid, "replay-001", CancellationToken.None);
        var replay = await _service.CreateAsync(Valid, "replay-001", CancellationToken.None);

        Assert.True(replay.Replayed);
        Assert.Equal(first.Application.Id, replay.Application.Id);
        Assert.Equal("process-001", replay.Application.ProcessInstanceKey);
    }

    [Fact]
    public async Task Create_rejects_reused_key_with_different_payload()
    {
        await _service.CreateAsync(Valid, "conflict-001", CancellationToken.None);
        var changed = Valid with { RequestedAmount = Valid.RequestedAmount + 1_000m };

        var exception = await Assert.ThrowsAsync<IdempotencyConflictException>(
            () => _service.CreateAsync(changed, "conflict-001", CancellationToken.None));

        Assert.Equal("IDEMPOTENCY_KEY_REUSED", exception.Code);
    }

    [Fact]
    public async Task Create_records_recoverable_failure_when_workflow_start_fails()
    {
        var service = new ApplicationService(_db, new FailingWorkflowGateway(), TimeProvider.System);

        var exception = await Assert.ThrowsAsync<WorkflowStartException>(
            () => service.CreateAsync(Valid, "start-failure-001", CancellationToken.None));

        var stored = await service.GetAsync(exception.ApplicationId, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(ApplicationStatus.StartFailed, stored.Status);
        Assert.Contains(
            await service.GetTimelineAsync(exception.ApplicationId, CancellationToken.None),
            item => item.EventType == "WORKFLOW_START_FAILED");
    }

    [Fact]
    public async Task Withdraw_updates_status_and_timeline()
    {
        var created = await _service.CreateAsync(Valid, "withdraw-001", CancellationToken.None);

        var withdrawn = await _service.WithdrawAsync(created.Application.Id, CancellationToken.None);

        Assert.Equal(ApplicationStatus.Withdrawn, withdrawn.Status);
        Assert.Equal("APPLICATION_WITHDRAWN", (await _service.GetTimelineAsync(withdrawn.Id, CancellationToken.None))[^1].EventType);
    }

    [Fact]
    public async Task Withdraw_after_context_reload_preserves_audit_sequence()
    {
        var created = await _service.CreateAsync(Valid, "withdraw-reload-001", CancellationToken.None);
        _db.ChangeTracker.Clear();

        var withdrawn = await _service.WithdrawAsync(created.Application.Id, CancellationToken.None);
        var timeline = await _service.GetTimelineAsync(withdrawn.Id, CancellationToken.None);

        Assert.Equal(ApplicationStatus.Withdrawn, withdrawn.Status);
        Assert.Equal(new long[] { 1, 2, 3 }, timeline.Select(item => item.Sequence));
    }

    private sealed class SingleStartWorkflowGateway : IWorkflowGateway
    {
        private int _starts;

        public Task<string> StartApplicationAsync(
            DomainApplicationId id,
            CreateApplicationCommand command,
            CancellationToken cancellationToken)
        {
            _starts++;
            if (_starts > 1)
            {
                throw new InvalidOperationException("A duplicate workflow was started.");
            }

            return Task.FromResult("process-001");
        }

        public Task CorrelateWithdrawalAsync(DomainApplicationId id, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FailingWorkflowGateway : IWorkflowGateway
    {
        public Task<string> StartApplicationAsync(
            DomainApplicationId id,
            CreateApplicationCommand command,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("Camunda unavailable.");

        public Task CorrelateWithdrawalAsync(DomainApplicationId id, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
