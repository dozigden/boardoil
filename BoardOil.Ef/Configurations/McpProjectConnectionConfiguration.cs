using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class McpProjectConnectionConfiguration : IEntityTypeConfiguration<EntityMcpProjectConnection>
{
    public void Configure(EntityTypeBuilder<EntityMcpProjectConnection> connection)
    {
        connection.HasKey(x => x.Id);
        connection.Property(x => x.PublicId).HasMaxLength(64).IsRequired();
        connection.Property(x => x.Name).HasMaxLength(120).IsRequired();
        connection.Property(x => x.AllowedScopesCsv).HasMaxLength(100).IsRequired();
        connection.Property(x => x.CreatedByUserName).HasMaxLength(64).IsRequired();
        connection.Property(x => x.RevokedByUserName).HasMaxLength(64).IsRequired(false);
        connection.Property(x => x.CreatedAtUtc).IsRequired();
        connection.Property(x => x.UpdatedAtUtc).IsRequired();
        connection.Property(x => x.RevokedAtUtc).IsRequired(false);
        connection.ToTable("McpProjectConnections");

        connection.HasIndex(x => x.PublicId).IsUnique();
        connection.HasIndex(x => x.ClientAccountId);
        connection.HasIndex(x => x.RevokedAtUtc);

        connection.HasOne(x => x.ClientAccount)
            .WithMany()
            .HasForeignKey(x => x.ClientAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        connection.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
        connection.HasOne(x => x.RevokedByUser)
            .WithMany()
            .HasForeignKey(x => x.RevokedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
