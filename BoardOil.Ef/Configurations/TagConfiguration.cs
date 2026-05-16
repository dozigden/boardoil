using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class TagConfiguration : IEntityTypeConfiguration<EntityTag>
{
    public void Configure(EntityTypeBuilder<EntityTag> tag)
    {
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
    }
}
