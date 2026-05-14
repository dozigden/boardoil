using BoardOil.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class CardTagConfiguration : IEntityTypeConfiguration<EntityCardTag>
{
    public void Configure(EntityTypeBuilder<EntityCardTag> cardTag)
    {
        cardTag.HasKey(x => x.Id);
        cardTag.Property(x => x.TagId).IsRequired();
        cardTag.ToTable("CardTags");
        cardTag.HasIndex(x => new { x.CardId, x.TagId }).IsUnique();
        cardTag.HasIndex(x => x.TagId);
    }
}
