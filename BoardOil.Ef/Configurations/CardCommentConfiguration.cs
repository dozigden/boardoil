using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class CardCommentConfiguration : IEntityTypeConfiguration<EntityCardComment>
{
    public void Configure(EntityTypeBuilder<EntityCardComment> cardComment)
    {
        cardComment.HasKey(x => x.Id);
        cardComment.Property(x => x.CardId).IsRequired();
        cardComment.Property(x => x.AuthorUserId).IsRequired(false);
        cardComment.Property(x => x.Text).HasMaxLength(4_000).IsRequired();
        cardComment.Property(x => x.PostedAtUtc).IsRequired();
        cardComment.Property(x => x.CreatedAtUtc).IsRequired();
        cardComment.ToTable("CardComments");
        cardComment.HasIndex(x => new { x.CardId, x.PostedAtUtc, x.Id });
        cardComment.HasIndex(x => x.AuthorUserId);
    }
}
