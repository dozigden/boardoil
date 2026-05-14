using BoardOil.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef;

public sealed class BoardOilDbContext(DbContextOptions<BoardOilDbContext> options) : DbContext(options)
{
    public DbSet<EntityBoard> Boards => Set<EntityBoard>();
    public DbSet<EntityBoardColumn> Columns => Set<EntityBoardColumn>();
    public DbSet<EntityBoardCard> Cards => Set<EntityBoardCard>();
    public DbSet<EntityArchivedCard> ArchivedCards => Set<EntityArchivedCard>();
    public DbSet<EntityCardType> CardTypes => Set<EntityCardType>();
    public DbSet<EntityTag> Tags => Set<EntityTag>();
    public DbSet<EntityCardTag> CardTags => Set<EntityCardTag>();
    public DbSet<EntityCardComment> CardComments => Set<EntityCardComment>();
    public DbSet<EntityBoardMember> BoardMembers => Set<EntityBoardMember>();
    public DbSet<EntityUser> Users => Set<EntityUser>();
    public DbSet<EntityRefreshToken> RefreshTokens => Set<EntityRefreshToken>();
    public DbSet<EntityPersonalAccessToken> PersonalAccessTokens => Set<EntityPersonalAccessToken>();
    public DbSet<EntityAppSetting> AppSettings => Set<EntityAppSetting>();
    public DbSet<EntityImage> Images => Set<EntityImage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BoardOilDbContext).Assembly);
    }
}
