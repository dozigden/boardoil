using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef;

public sealed class BoardOilDbContext(DbContextOptions<BoardOilDbContext> options) : DbContext(options)
{
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
    public DbSet<EntityMcpProjectConnection> McpProjectConnections => Set<EntityMcpProjectConnection>();
    public DbSet<EntityAppSetting> AppSettings => Set<EntityAppSetting>();
    public DbSet<EntitySystemInfoMessage> SystemInfoMessages => Set<EntitySystemInfoMessage>();
    public DbSet<EntityImage> Images => Set<EntityImage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BoardOilDbContext).Assembly);
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
}
