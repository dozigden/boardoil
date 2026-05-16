using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class BoardColumnConfiguration : IEntityTypeConfiguration<EntityBoardColumn>
{
    public void Configure(EntityTypeBuilder<EntityBoardColumn> column)
    {
        column.HasKey(x => x.Id);
        column.Property(x => x.Title).HasMaxLength(200).IsRequired();
        column.Property(x => x.SortKey).HasMaxLength(20).IsRequired();
        column.ToTable("Columns");
        column.HasIndex(x => new { x.BoardId, x.SortKey }).IsUnique();
        column.HasMany(x => x.Cards)
            .WithOne(x => x.BoardColumn)
            .HasForeignKey(x => x.BoardColumnId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
