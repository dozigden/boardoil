using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class CardTypeConfiguration : IEntityTypeConfiguration<EntityCardType>
{
    public void Configure(EntityTypeBuilder<EntityCardType> cardType)
    {
        cardType.HasKey(x => x.Id);
        cardType.Property(x => x.BoardId).IsRequired();
        cardType.Property(x => x.Name).HasMaxLength(40).IsRequired();
        cardType.Property(x => x.Emoji).HasMaxLength(32).IsRequired(false);
        cardType.Property(x => x.StyleName).HasMaxLength(64).IsRequired();
        cardType.Property(x => x.StylePropertiesJson).IsRequired();
        cardType.Property(x => x.IsSystem).IsRequired();
        cardType.ToTable("CardTypes");
        cardType.HasIndex(x => x.BoardId);
        cardType.HasIndex(x => x.BoardId)
            .HasFilter("\"IsSystem\" = 1")
            .IsUnique();
    }
}
