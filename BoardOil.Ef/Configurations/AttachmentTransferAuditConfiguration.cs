using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class AttachmentTransferAuditConfiguration : IEntityTypeConfiguration<EntityAttachmentTransferAudit>
{
    public void Configure(EntityTypeBuilder<EntityAttachmentTransferAudit> audit)
    {
        audit.ToTable("AttachmentTransferAudits");
        audit.HasKey(x => x.Id);
        audit.HasIndex(x => x.TicketId);
        audit.HasIndex(x => x.OccurredAtUtc);
        audit.HasIndex(x => new { x.Operation, x.TicketId });
        audit.HasIndex(x => new { x.Outcome, x.OccurredAtUtc });

        // Deliberately no foreign keys: audit rows must survive deletion of the ticket,
        // attachment, board, user or originating credential that they describe.
    }
}
