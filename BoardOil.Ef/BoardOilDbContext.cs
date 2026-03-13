using BoardOil.Ef.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef;

public sealed class BoardOilDbContext(DbContextOptions<BoardOilDbContext> options) : DbContext(options)
{
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardColumn> Columns => Set<BoardColumn>();
    public DbSet<BoardCard> Cards => Set<BoardCard>();

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
        column.HasIndex(x => new { x.BoardId, x.Position }).IsUnique();
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
    }
}
