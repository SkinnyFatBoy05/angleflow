using Microsoft.EntityFrameworkCore;

namespace AngleFlow.Infrastructure.Persistence;

public sealed class AngleFlowDbContext(DbContextOptions<AngleFlowDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationRecord> Applications => Set<ApplicationRecord>();
    public DbSet<AuditRecord> AuditEvents => Set<AuditRecord>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<CheckRecord> Checks => Set<CheckRecord>();
    public DbSet<OfferRecord> Offers => Set<OfferRecord>();
    public DbSet<NotificationRecord> Notifications => Set<NotificationRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var application = modelBuilder.Entity<ApplicationRecord>();
        application.HasKey(item => item.Id);
        application.Property(item => item.ApplicantName).HasMaxLength(120);
        application.Property(item => item.Email).HasMaxLength(254);
        application.Property(item => item.AssetType).HasMaxLength(40);
        application.Property(item => item.Scenario).HasMaxLength(40);
        application.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
        application.Property(item => item.ProcessInstanceKey).HasMaxLength(80);
        application.Property(item => item.Version).IsConcurrencyToken();

        var audit = modelBuilder.Entity<AuditRecord>();
        audit.HasKey(item => item.Id);
        audit.HasIndex(item => new { item.ApplicationId, item.Sequence }).IsUnique();
        audit.Property(item => item.EventType).HasMaxLength(80);
        audit.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
        audit.Property(item => item.ReasonCode).HasMaxLength(80);
        audit.HasOne(item => item.Application)
            .WithMany(item => item.AuditEvents)
            .HasForeignKey(item => item.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        var idempotency = modelBuilder.Entity<IdempotencyRecord>();
        idempotency.HasKey(item => item.Key);
        idempotency.Property(item => item.Key).HasMaxLength(128);
        idempotency.Property(item => item.PayloadHash).HasMaxLength(64);
        idempotency.HasIndex(item => item.ApplicationId);

        ConfigureExternalRecord(modelBuilder.Entity<CheckRecord>());
        ConfigureExternalRecord(modelBuilder.Entity<OfferRecord>());
        ConfigureExternalRecord(modelBuilder.Entity<NotificationRecord>());
    }

    private static void ConfigureExternalRecord<TEntity>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> builder)
        where TEntity : class
    {
        builder.HasKey("Id");
        builder.HasIndex("ExternalOperationKey").IsUnique();
        builder.Property<string>("ExternalOperationKey").HasMaxLength(160);
    }
}
