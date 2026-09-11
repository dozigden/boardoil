using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class AttachmentDownloadTicketConfiguration : IEntityTypeConfiguration<EntityAttachmentDownloadTicket>
{
    public void Configure(EntityTypeBuilder<EntityAttachmentDownloadTicket> ticket)
    {
        ticket.ToTable("AttachmentDownloadTickets", table =>
        {
            table.HasCheckConstraint("CK_AttachmentDownloadTickets_Owner",
                "(CardId IS NOT NULL AND ArchivedCardId IS NULL) OR (CardId IS NULL AND ArchivedCardId IS NOT NULL)");
            table.HasCheckConstraint("CK_AttachmentDownloadTickets_Credential",
                "(PersonalAccessTokenId IS NOT NULL AND OAuthTokenId IS NULL AND OAuthAuthorizationId IS NULL) OR (PersonalAccessTokenId IS NULL AND OAuthTokenId IS NOT NULL AND OAuthAuthorizationId IS NOT NULL)");
        });
        ticket.HasKey(x => x.Id);
        ticket.Property(x => x.SecretHash).HasMaxLength(64).IsRequired();
        ticket.Property(x => x.OAuthTokenId).HasMaxLength(100);
        ticket.Property(x => x.OAuthAuthorizationId).HasMaxLength(100);
        ticket.HasIndex(x => x.ExpiresAtUtc);
        ticket.HasOne(x => x.Attachment).WithMany().HasForeignKey(x => x.AttachmentId).OnDelete(DeleteBehavior.Cascade);
    }
}
