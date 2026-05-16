using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class ImageConfiguration : IEntityTypeConfiguration<EntityImage>
{
    public void Configure(EntityTypeBuilder<EntityImage> image)
    {
        image.HasKey(x => x.Id);
        image.Property(x => x.EntityType).IsRequired();
        image.Property(x => x.EntityId).IsRequired();
        image.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
        image.Property(x => x.ContentType).HasMaxLength(200).IsRequired();
        image.Property(x => x.RelativePath).HasMaxLength(1024).IsRequired();
        image.Property(x => x.ByteLength).IsRequired();
        image.Property(x => x.Width).IsRequired(false);
        image.Property(x => x.Height).IsRequired(false);
        image.Property(x => x.CreatedAtUtc).IsRequired();
        image.Property(x => x.UpdatedAtUtc).IsRequired();
        image.ToTable("Images");
        image.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}
