using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class BoardConfiguration : IEntityTypeConfiguration<EntityBoard>
{
    public void Configure(EntityTypeBuilder<EntityBoard> board)
    {
        board.HasKey(x => x.Id);
        board.Property(x => x.Name).HasMaxLength(120).IsRequired();
        board.Property(x => x.Description).HasMaxLength(5_000).IsRequired();
        board.Property(x => x.SlickCohesionModeEnabled).HasDefaultValue(true).IsRequired();
        board.ToTable("Boards");
        board.HasOne(x => x.CardIdSequence)
            .WithOne(x => x.Board)
            .HasForeignKey<EntityBoardCardIdSequence>(x => x.BoardId)
            .OnDelete(DeleteBehavior.Cascade);
        board.HasMany(x => x.Columns)
            .WithOne(x => x.Board)
            .HasForeignKey(x => x.BoardId)
            .OnDelete(DeleteBehavior.Cascade);
        board.HasMany(x => x.CardTypes)
            .WithOne(x => x.Board)
            .HasForeignKey(x => x.BoardId)
            .OnDelete(DeleteBehavior.Cascade);
        board.HasMany(x => x.ArchivedCards)
            .WithOne(x => x.Board)
            .HasForeignKey(x => x.BoardId)
            .OnDelete(DeleteBehavior.Cascade);
        board.HasMany(x => x.Tags)
            .WithOne(x => x.Board)
            .HasForeignKey(x => x.BoardId)
            .OnDelete(DeleteBehavior.Cascade);
        board.HasMany(x => x.Slicks)
            .WithOne(x => x.Board)
            .HasForeignKey(x => x.BoardId)
            .OnDelete(DeleteBehavior.Cascade);
        board.HasMany(x => x.Members)
            .WithOne(x => x.Board)
            .HasForeignKey(x => x.BoardId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
