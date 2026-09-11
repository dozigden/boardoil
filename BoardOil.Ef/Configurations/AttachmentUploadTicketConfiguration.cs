using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class AttachmentUploadTicketConfiguration : IEntityTypeConfiguration<EntityAttachmentUploadTicket>
{
    public void Configure(EntityTypeBuilder<EntityAttachmentUploadTicket> ticket)
    {
        ticket.ToTable("AttachmentUploadTickets", table =>
        {
            table.HasCheckConstraint("CK_AttachmentUploadTickets_Credential",
                "(PersonalAccessTokenId IS NOT NULL AND OAuthTokenId IS NULL AND OAuthAuthorizationId IS NULL) OR (PersonalAccessTokenId IS NULL AND OAuthTokenId IS NOT NULL AND OAuthAuthorizationId IS NOT NULL)");
            table.HasCheckConstraint("CK_AttachmentUploadTickets_State", "State IN (0, 1, 2, 3)");
            table.HasCheckConstraint("CK_AttachmentUploadTickets_ByteLength", "DeclaredByteLength >= 0");
        });
        ticket.HasKey(x => x.Id);
        ticket.Property(x => x.SecretHash).HasMaxLength(64).IsRequired();
        ticket.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        ticket.Property(x => x.ContentType).HasMaxLength(255).IsRequired();
        ticket.Property(x => x.OAuthTokenId).HasMaxLength(100);
        ticket.Property(x => x.OAuthAuthorizationId).HasMaxLength(100);
        ticket.HasIndex(x => x.ExpiresAtUtc);
        ticket.HasIndex(x => x.State);
        ticket.HasIndex(x => x.AttachmentId).IsUnique();
        ticket.HasOne(x => x.Attachment).WithMany().HasForeignKey(x => x.AttachmentId).OnDelete(DeleteBehavior.Cascade);
    }
}
