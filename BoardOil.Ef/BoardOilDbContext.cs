using BoardOil.Abstractions.Entities;
using BoardOil.Ef.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef;

public sealed class BoardOilDbContext(DbContextOptions<BoardOilDbContext> options) : DbContext(options)
{
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardColumn> Columns => Set<BoardColumn>();
    public DbSet<BoardCard> Cards => Set<BoardCard>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<CardTag> CardTags => Set<CardTag>();
    public DbSet<BoardUser> Users => Set<BoardUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var board = modelBuilder.Entity<Board>();
        board.HasKey(x => x.Id);
        board.Property(x => x.Name).HasMaxLength(120).IsRequired();
        board.HasMany(x => x.Columns)
            .WithOne(x => x.Board)
            .HasForeignKey(x => x.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        var column = modelBuilder.Entity<BoardColumn>();
        column.HasKey(x => x.Id);
        column.Property(x => x.Title).HasMaxLength(200).IsRequired();
        column.Property(x => x.SortKey).HasMaxLength(20).IsRequired();
        column.HasIndex(x => new { x.BoardId, x.SortKey }).IsUnique();
        column.HasMany(x => x.Cards)
            .WithOne(x => x.BoardColumn)
            .HasForeignKey(x => x.BoardColumnId)
            .OnDelete(DeleteBehavior.Cascade);

        var card = modelBuilder.Entity<BoardCard>();
        card.HasKey(x => x.Id);
        card.Property(x => x.Title).HasMaxLength(200).IsRequired();
        card.Property(x => x.Description).HasMaxLength(5000).IsRequired();
        card.Property(x => x.SortKey).HasMaxLength(20).IsRequired();
        card.HasIndex(x => new { x.BoardColumnId, x.SortKey }).IsUnique();
        card.HasMany(x => x.CardTags)
            .WithOne(x => x.Card)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        var cardTag = modelBuilder.Entity<CardTag>();
        cardTag.HasKey(x => new { x.CardId, x.TagName });
        cardTag.Property(x => x.TagName).HasMaxLength(40).IsRequired();
        cardTag.HasIndex(x => x.TagName);

        var tag = modelBuilder.Entity<Tag>();
        tag.HasKey(x => x.Id);
        tag.Property(x => x.Name).HasMaxLength(40).IsRequired();
        tag.Property(x => x.NormalisedName).HasMaxLength(40).IsRequired();
        tag.Property(x => x.StyleName).HasMaxLength(32).IsRequired();
        tag.Property(x => x.StylePropertiesJson).IsRequired();
        tag.HasIndex(x => x.NormalisedName).IsUnique();

        var user = modelBuilder.Entity<BoardUser>();
        user.HasKey(x => x.Id);
        user.Property(x => x.UserName).HasMaxLength(64).IsRequired();
        user.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        user.Property(x => x.Role).IsRequired();
        user.Property(x => x.IsActive).IsRequired();
        user.HasIndex(x => x.UserName).IsUnique();
        user.HasMany(x => x.RefreshTokens)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        var refreshToken = modelBuilder.Entity<RefreshToken>();
        refreshToken.HasKey(x => x.Id);
        refreshToken.Property(x => x.TokenHash).HasMaxLength(200).IsRequired();
        refreshToken.Property(x => x.ExpiresAtUtc).IsRequired();
        refreshToken.Property(x => x.CreatedAtUtc).IsRequired();
        refreshToken.Property(x => x.RevokedAtUtc).IsRequired(false);
        refreshToken.Property(x => x.ReplacedByTokenHash).HasMaxLength(200).IsRequired(false);
        refreshToken.HasIndex(x => x.TokenHash).IsUnique();
    }
}
