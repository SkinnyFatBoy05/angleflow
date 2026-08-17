using AngleFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainApplicationId = AngleFlow.Domain.Applications.ApplicationId;

namespace AngleFlow.Infrastructure.Services;

public sealed class ExternalOperationService(AngleFlowDbContext db, TimeProvider timeProvider)
{
    public async Task<string> CreateOfferAsync(
        DomainApplicationId applicationId,
        string operationKey,
        CancellationToken cancellationToken)
    {
        var existing = await db.Offers.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ExternalOperationKey == operationKey, cancellationToken);
        if (existing is not null)
        {
            return existing.OfferNumber;
        }

        var offerNumber = $"AF-{applicationId.Value:N}"[..15].ToUpperInvariant();
        db.Offers.Add(new()
        {
            ApplicationId = applicationId.Value,
            OfferNumber = offerNumber,
            ExternalOperationKey = operationKey,
            CreatedAt = timeProvider.GetUtcNow()
        });
        await db.SaveChangesAsync(cancellationToken);
        return offerNumber;
    }

    public async Task SendNotificationAsync(
        DomainApplicationId applicationId,
        string template,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (await db.Notifications.AsNoTracking()
            .AnyAsync(item => item.ExternalOperationKey == operationKey, cancellationToken))
        {
            return;
        }

        db.Notifications.Add(new()
        {
            ApplicationId = applicationId.Value,
            Template = template,
            ExternalOperationKey = operationKey,
            SentAt = timeProvider.GetUtcNow()
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
