using BoardOil.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef;

public sealed class BoardOilDbContext(DbContextOptions<BoardOilDbContext> options) : DbContext(options)
{
    public DbSet<EntityBoard> Boards => Set<EntityBoard>();
    public DbSet<EntityBoardColumn> Columns => Set<EntityBoardColumn>();
    public DbSet<EntityBoardCard> Cards => Set<EntityBoardCard>();
    public DbSet<EntityCardType> CardTypes => Set<EntityCardType>();
    public DbSet<EntityTag> Tags => Set<EntityTag>();
    public DbSet<EntityCardTag> CardTags => Set<EntityCardTag>();
    public DbSet<EntityBoardMember> BoardMembers => Set<EntityBoardMember>();
    public DbSet<EntityUser> Users => Set<EntityUser>();
    public DbSet<EntityRefreshToken> RefreshTokens => Set<EntityRefreshToken>();
    public DbSet<EntityPersonalAccessToken> PersonalAccessTokens => Set<EntityPersonalAccessToken>();
    public DbSet<EntityAppSetting> AppSettings => Set<EntityAppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var board = modelBuilder.Entity<EntityBoard>();
        board.HasKey(x => x.Id);
        board.Property(x => x.Name).HasMaxLength(120).IsRequired();
        board.ToTable("Boards");
        board.HasMany(x => x.Columns)
            .WithOne(x => x.Board)
            .HasForeignKey(x => x.BoardId)
            .OnDelete(DeleteBehavior.Cascade);
        board.HasMany(x => x.CardTypes)
            .WithOne(x => x.Board)
            .HasForeignKey(x => x.BoardId)
            .OnDelete(DeleteBehavior.Cascade);
        board.HasMany(x => x.Tags)
            .WithOne(x => x.Board)
            .HasForeignKey(x => x.BoardId)
            .OnDelete(DeleteBehavior.Cascade);
        board.HasMany(x => x.Members)
            .WithOne(x => x.Board)
            .HasForeignKey(x => x.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        var column = modelBuilder.Entity<EntityBoardColumn>();
        column.HasKey(x => x.Id);
        column.Property(x => x.Title).HasMaxLength(200).IsRequired();
        column.Property(x => x.SortKey).HasMaxLength(20).IsRequired();
        column.ToTable("Columns");
        column.HasIndex(x => new { x.BoardId, x.SortKey }).IsUnique();
        column.HasMany(x => x.Cards)
            .WithOne(x => x.BoardColumn)
            .HasForeignKey(x => x.BoardColumnId)
            .OnDelete(DeleteBehavior.Cascade);

        var card = modelBuilder.Entity<EntityBoardCard>();
        card.HasKey(x => x.Id);
        card.Property(x => x.CardTypeId).IsRequired();
        card.Property(x => x.Title).HasMaxLength(200).IsRequired();
        card.Property(x => x.Description).HasMaxLength(5000).IsRequired();
        card.Property(x => x.SortKey).HasMaxLength(20).IsRequired();
        card.ToTable("Cards");
        card.HasIndex(x => new { x.BoardColumnId, x.SortKey }).IsUnique();
        card.HasIndex(x => x.CardTypeId);
        card.HasOne(x => x.CardType)
            .WithMany(x => x.Cards)
            .HasForeignKey(x => x.CardTypeId)
            .OnDelete(DeleteBehavior.NoAction);
        card.HasMany(x => x.CardTags)
            .WithOne(x => x.Card)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        var cardType = modelBuilder.Entity<EntityCardType>();
        cardType.HasKey(x => x.Id);
        cardType.Property(x => x.BoardId).IsRequired();
        cardType.Property(x => x.Name).HasMaxLength(40).IsRequired();
        cardType.Property(x => x.Emoji).HasMaxLength(32).IsRequired(false);
        cardType.Property(x => x.IsSystem).IsRequired();
        cardType.ToTable("CardTypes");
        cardType.HasIndex(x => x.BoardId);
        cardType.HasIndex(x => x.BoardId)
            .HasFilter("\"IsSystem\" = 1")
            .IsUnique();

        var cardTag = modelBuilder.Entity<EntityCardTag>();
        cardTag.HasKey(x => x.Id);
        cardTag.Property(x => x.TagId).IsRequired();
        cardTag.ToTable("CardTags");
        cardTag.HasIndex(x => new { x.CardId, x.TagId }).IsUnique();
        cardTag.HasIndex(x => x.TagId);

        var tag = modelBuilder.Entity<EntityTag>();
        tag.HasKey(x => x.Id);
        tag.Property(x => x.BoardId).IsRequired();
        tag.Property(x => x.Name).HasMaxLength(40).IsRequired();
        tag.Property(x => x.NormalisedName).HasMaxLength(40).IsRequired();
        tag.Property(x => x.StyleName).HasMaxLength(32).IsRequired();
        tag.Property(x => x.StylePropertiesJson).IsRequired();
        tag.Property(x => x.Emoji).HasMaxLength(32).IsRequired(false);
        tag.ToTable("Tags");
        tag.HasIndex(x => x.BoardId);
        tag.HasIndex(x => new { x.BoardId, x.NormalisedName }).IsUnique();
        tag.HasMany(x => x.CardTags)
            .WithOne(x => x.Tag)
            .HasForeignKey(x => x.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        var user = modelBuilder.Entity<EntityUser>();
        user.HasKey(x => x.Id);
        user.Property(x => x.UserName).HasMaxLength(64).IsRequired();
        user.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        user.Property(x => x.Role).IsRequired();
        user.Property(x => x.IsActive).IsRequired();
        user.ToTable("Users");
        user.HasIndex(x => x.UserName).IsUnique();
        user.HasMany(x => x.RefreshTokens)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        user.HasMany(x => x.PersonalAccessTokens)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        user.HasMany(x => x.BoardMemberships)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        var boardMember = modelBuilder.Entity<EntityBoardMember>();
        boardMember.HasKey(x => x.Id);
        boardMember.Property(x => x.Role).IsRequired();
        boardMember.Property(x => x.CreatedAtUtc).IsRequired();
        boardMember.Property(x => x.UpdatedAtUtc).IsRequired();
        boardMember.ToTable("BoardMembers");
        boardMember.HasIndex(x => new { x.BoardId, x.UserId }).IsUnique();
        boardMember.HasIndex(x => new { x.BoardId, x.Role });

        var refreshToken = modelBuilder.Entity<EntityRefreshToken>();
        refreshToken.HasKey(x => x.Id);
        refreshToken.Property(x => x.TokenHash).HasMaxLength(200).IsRequired();
        refreshToken.Property(x => x.ExpiresAtUtc).IsRequired();
        refreshToken.Property(x => x.CreatedAtUtc).IsRequired();
        refreshToken.Property(x => x.RevokedAtUtc).IsRequired(false);
        refreshToken.Property(x => x.ReplacedByTokenHash).HasMaxLength(200).IsRequired(false);
        refreshToken.ToTable("RefreshTokens");
        refreshToken.HasIndex(x => x.TokenHash).IsUnique();

        var personalAccessToken = modelBuilder.Entity<EntityPersonalAccessToken>();
        personalAccessToken.HasKey(x => x.Id);
        personalAccessToken.Property(x => x.Name).HasMaxLength(120).IsRequired();
        personalAccessToken.Property(x => x.TokenHash).HasMaxLength(200).IsRequired();
        personalAccessToken.Property(x => x.TokenPrefix).HasMaxLength(24).IsRequired();
        personalAccessToken.Property(x => x.ScopesCsv).HasMaxLength(500).IsRequired();
        personalAccessToken.Property(x => x.BoardAccessMode).HasMaxLength(24).IsRequired();
        personalAccessToken.Property(x => x.AllowedBoardIdsCsv).HasMaxLength(500).IsRequired();
        personalAccessToken.Property(x => x.CreatedAtUtc).IsRequired();
        personalAccessToken.Property(x => x.ExpiresAtUtc).IsRequired(false);
        personalAccessToken.Property(x => x.LastUsedAtUtc).IsRequired(false);
        personalAccessToken.Property(x => x.RevokedAtUtc).IsRequired(false);
        personalAccessToken.ToTable("PersonalAccessTokens");
        personalAccessToken.HasIndex(x => x.TokenHash).IsUnique();
        personalAccessToken.HasIndex(x => x.UserId);

        var appSetting = modelBuilder.Entity<EntityAppSetting>();
        appSetting.HasKey(x => x.Id);
        appSetting.Property(x => x.Key).HasMaxLength(120).IsRequired();
        appSetting.Property(x => x.Value).HasMaxLength(4000).IsRequired();
        appSetting.Property(x => x.UpdatedAtUtc).IsRequired();
        appSetting.ToTable("AppSettings");
        appSetting.HasIndex(x => x.Key).IsUnique();
    }
}
