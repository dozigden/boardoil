using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class CardAttachmentConfiguration : IEntityTypeConfiguration<EntityCardAttachment>
{
    public void Configure(EntityTypeBuilder<EntityCardAttachment> attachment)
    {
        attachment.ToTable("CardAttachments", table => table.HasCheckConstraint(
            "CK_CardAttachments_ExactlyOneOwner",
            "(State IN (0, 1, 2)) AND NOT (CardId IS NOT NULL AND ArchivedCardId IS NOT NULL) AND (State != 1 OR CardId IS NOT NULL OR ArchivedCardId IS NOT NULL) AND (State != 2 OR (CardId IS NULL AND ArchivedCardId IS NULL))"));
        attachment.HasKey(x => x.Id);
        attachment.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        attachment.Property(x => x.NormalisedFileName).HasMaxLength(255).IsRequired();
        attachment.HasIndex(x => new { x.CardId, x.NormalisedFileName }).IsUnique();
        attachment.HasIndex(x => new { x.ArchivedCardId, x.NormalisedFileName }).IsUnique();
        attachment.Property(x => x.ContentType).HasMaxLength(255).IsRequired();
        attachment.Property(x => x.StorageKey).HasMaxLength(32).IsRequired();
        attachment.Property(x => x.ThumbnailStorageKey).HasMaxLength(32);
        attachment.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        attachment.HasIndex(x => x.StorageKey).IsUnique();
        attachment.HasIndex(x => x.ThumbnailStorageKey).IsUnique();
        attachment.HasIndex(x => x.State);
        attachment.HasOne(x => x.Card).WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Restrict);
        attachment.HasOne(x => x.ArchivedCard).WithMany().HasForeignKey(x => x.ArchivedCardId).OnDelete(DeleteBehavior.Restrict);
        attachment.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class TemporaryBoardPackageConfiguration : IEntityTypeConfiguration<EntityTemporaryBoardPackage>
{
    public void Configure(EntityTypeBuilder<EntityTemporaryBoardPackage> cleanup)
    {
        cleanup.ToTable("TemporaryBoardPackages");
        cleanup.HasKey(x => x.Id);
        cleanup.Property(x => x.StorageKey).HasMaxLength(32).IsRequired();
        cleanup.HasIndex(x => x.StorageKey).IsUnique();
    }
}
