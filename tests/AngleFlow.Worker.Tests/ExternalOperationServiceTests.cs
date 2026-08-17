using AngleFlow.Infrastructure.Persistence;
using AngleFlow.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using DomainApplicationId = AngleFlow.Domain.Applications.ApplicationId;

namespace AngleFlow.Worker.Tests;

public sealed class ExternalOperationServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private AngleFlowDbContext _db = null!;
    private ExternalOperationService _service = null!;
    private static readonly DomainApplicationId Id = new(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AngleFlowDbContext>().UseSqlite(_connection).Options;
        _db = new AngleFlowDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _service = new ExternalOperationService(_db, TimeProvider.System);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Create_offer_replays_without_duplicate_side_effect()
    {
        var first = await _service.CreateOfferAsync(Id, "offer-op-001", CancellationToken.None);
        var replay = await _service.CreateOfferAsync(Id, "offer-op-001", CancellationToken.None);

        Assert.Equal(first, replay);
        Assert.Equal(1, await _db.Offers.CountAsync());
    }

    [Fact]
    public async Task Send_notification_replays_without_duplicate_side_effect()
    {
        await _service.SendNotificationAsync(Id, "approved", "notify-op-001", CancellationToken.None);
        await _service.SendNotificationAsync(Id, "approved", "notify-op-001", CancellationToken.None);

        Assert.Equal(1, await _db.Notifications.CountAsync());
    }
}
