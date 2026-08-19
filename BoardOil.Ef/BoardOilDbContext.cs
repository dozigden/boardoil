using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BoardOil.Ef;

public sealed class BoardOilDbContext(DbContextOptions<BoardOilDbContext> options) : DbContext(options)
{
    private static readonly ValueConverter<DateTime, DateTime> UtcDateTimeConverter = new(
        value => value.Kind == DateTimeKind.Local
            ? value.ToUniversalTime()
            : DateTime.SpecifyKind(value, DateTimeKind.Utc),
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public DbSet<EntityBoard> Boards => Set<EntityBoard>();
    public DbSet<EntityBoardCardIdSequence> BoardCardIdSequences => Set<EntityBoardCardIdSequence>();
    public DbSet<EntityBoardColumn> Columns => Set<EntityBoardColumn>();
    public DbSet<EntityBoardCard> Cards => Set<EntityBoardCard>();
    public DbSet<EntityArchivedCard> ArchivedCards => Set<EntityArchivedCard>();
    public DbSet<EntityCardType> CardTypes => Set<EntityCardType>();
    public DbSet<EntityTag> Tags => Set<EntityTag>();
    public DbSet<EntitySlick> Slicks => Set<EntitySlick>();
    public DbSet<EntityCardTag> CardTags => Set<EntityCardTag>();
    public DbSet<EntityCardComment> CardComments => Set<EntityCardComment>();
    public DbSet<EntityBoardMember> BoardMembers => Set<EntityBoardMember>();
    public DbSet<EntityUser> Users => Set<EntityUser>();
    public DbSet<EntityRefreshToken> RefreshTokens => Set<EntityRefreshToken>();
    public DbSet<EntityPersonalAccessToken> PersonalAccessTokens => Set<EntityPersonalAccessToken>();
    public DbSet<EntityOAuthConnection> OAuthConnections => Set<EntityOAuthConnection>();
    public DbSet<EntityOAuthConnectionGrant> OAuthConnectionGrants => Set<EntityOAuthConnectionGrant>();
    public DbSet<EntityAppSetting> AppSettings => Set<EntityAppSetting>();
    public DbSet<EntitySystemInfoMessage> SystemInfoMessages => Set<EntitySystemInfoMessage>();
    public DbSet<EntityErrorLog> ErrorLogs => Set<EntityErrorLog>();
    public DbSet<EntityImage> Images => Set<EntityImage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseOpenIddict();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BoardOilDbContext).Assembly);
        ConfigureUtcDateTimeProperties(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyEntityTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyEntityTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyEntityTimestamps()
    {
        var nowUtc = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<ISupportCreatedAt>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedAtUtc == default)
            {
                entry.Property(nameof(ISupportCreatedAt.CreatedAtUtc)).CurrentValue = nowUtc;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ISupportUpdatedAt>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(ISupportUpdatedAt.UpdatedAtUtc)).CurrentValue = nowUtc;
                continue;
            }

            if (entry.State == EntityState.Added && entry.Entity.UpdatedAtUtc == default)
            {
                entry.Property(nameof(ISupportUpdatedAt.UpdatedAtUtc)).CurrentValue = nowUtc;
            }
        }
    }

    private static void ConfigureUtcDateTimeProperties(ModelBuilder modelBuilder)
    {
        var entityNamespace = typeof(EntityBoard).Namespace;
        var utcProperties = modelBuilder.Model.GetEntityTypes()
            .Where(entityType => string.Equals(entityType.ClrType.Namespace, entityNamespace, StringComparison.Ordinal))
            .SelectMany(entityType => entityType.GetProperties())
            .Where(property => property.Name.EndsWith("Utc", StringComparison.Ordinal))
            .Where(property => Nullable.GetUnderlyingType(property.ClrType) == typeof(DateTime)
                || property.ClrType == typeof(DateTime));

        foreach (var property in utcProperties)
        {
            property.SetValueConverter(UtcDateTimeConverter);
        }
    }
}
