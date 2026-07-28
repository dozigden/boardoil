using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class BoardCardConfiguration : IEntityTypeConfiguration<EntityBoardCard>
{
    public void Configure(EntityTypeBuilder<EntityBoardCard> card)
    {
        card.HasKey(x => x.Id);
        card.Property(x => x.CardTypeId).IsRequired();
        card.Property(x => x.AssignedUserId).IsRequired(false);
        card.Property(x => x.SlickId).IsRequired(false);
        card.Property(x => x.Title).HasMaxLength(200).IsRequired();
        card.Property(x => x.Description).HasMaxLength(20_000).IsRequired();
        card.Property(x => x.ExternalUrl).IsRequired(false);
        card.Property(x => x.SortKey).HasMaxLength(20).IsRequired();
        card.ToTable("Cards");
        card.HasIndex(x => new { x.BoardColumnId, x.SortKey }).IsUnique();
        card.HasIndex(x => x.CardTypeId);
        card.HasIndex(x => x.AssignedUserId);
        card.HasIndex(x => x.SlickId);
        card.HasOne(x => x.CardType)
            .WithMany(x => x.Cards)
            .HasForeignKey(x => x.CardTypeId)
            .OnDelete(DeleteBehavior.NoAction);
        card.HasOne(x => x.AssignedUser)
            .WithMany()
            .HasForeignKey(x => x.AssignedUserId)
            .OnDelete(DeleteBehavior.SetNull);
        card.HasOne(x => x.Slick)
            .WithMany(x => x.Cards)
            .HasForeignKey(x => x.SlickId)
            .OnDelete(DeleteBehavior.SetNull);
        card.HasMany(x => x.CardTags)
            .WithOne(x => x.Card)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);
        card.HasMany(x => x.Comments)
            .WithOne(x => x.Card)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
