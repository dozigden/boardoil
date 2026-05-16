using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class ArchivedCardConfiguration : IEntityTypeConfiguration<EntityArchivedCard>
{
    public void Configure(EntityTypeBuilder<EntityArchivedCard> archivedCard)
    {
        archivedCard.HasKey(x => x.Id);
        archivedCard.Property(x => x.BoardId).IsRequired();
        archivedCard.Property(x => x.OriginalCardId).IsRequired();
        archivedCard.Property(x => x.ArchivedAtUtc).IsRequired();
        archivedCard.Property(x => x.SnapshotJson).HasMaxLength(2_097_152).IsRequired();
        archivedCard.Property(x => x.SearchTitle).HasMaxLength(200).IsRequired();
        archivedCard.Property(x => x.SearchTagsJson).HasMaxLength(65_535).IsRequired();
        archivedCard.Property(x => x.SearchTextNormalised).HasMaxLength(65_535).IsRequired();
        archivedCard.ToTable("ArchivedCards");
        archivedCard.HasIndex(x => new { x.BoardId, x.ArchivedAtUtc, x.Id });
        archivedCard.HasIndex(x => x.OriginalCardId).IsUnique();
    }
}
