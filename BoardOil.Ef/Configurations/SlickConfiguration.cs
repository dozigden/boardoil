using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class SlickConfiguration : IEntityTypeConfiguration<EntitySlick>
{
    public void Configure(EntityTypeBuilder<EntitySlick> slick)
    {
        slick.HasKey(x => x.Id);
        slick.Property(x => x.BoardId).IsRequired();
        slick.Property(x => x.Name).HasMaxLength(40).IsRequired();
        slick.Property(x => x.NormalisedName).HasMaxLength(40).IsRequired();
        slick.Property(x => x.StyleName).HasMaxLength(32).IsRequired();
        slick.Property(x => x.StylePropertiesJson).IsRequired();
        slick.ToTable("Slicks");
        slick.HasIndex(x => x.BoardId);
        slick.HasIndex(x => new { x.BoardId, x.NormalisedName }).IsUnique();
        slick.HasMany(x => x.Cards)
            .WithOne(x => x.Slick)
            .HasForeignKey(x => x.SlickId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
