using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class OAuthConnectionConfiguration : IEntityTypeConfiguration<EntityOAuthConnection>
{
    public void Configure(EntityTypeBuilder<EntityOAuthConnection> connection)
    {
        connection.HasKey(x => x.Id);
        connection.Property(x => x.ResourceType).HasMaxLength(32).IsRequired();
        connection.Property(x => x.Name).HasMaxLength(120).IsRequired();
        connection.Property(x => x.NormalisedName).HasMaxLength(120).IsRequired();
        connection.Property(x => x.RevokedByUserName).HasMaxLength(64).IsRequired(false);
        connection.Property(x => x.CreatedAtUtc).IsRequired();
        connection.Property(x => x.UpdatedAtUtc).IsRequired();
        connection.Property(x => x.RevokedAtUtc).IsRequired(false);
        connection.ToTable("OAuthConnections");

        connection.HasIndex(x => new { x.UserId, x.ResourceType, x.NormalisedName }).IsUnique();
        connection.HasIndex(x => x.ActiveGrantId).IsUnique();
        connection.HasIndex(x => x.RevokedAtUtc);

        connection.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        connection.HasOne(x => x.ActiveGrant)
            .WithOne()
            .HasForeignKey<EntityOAuthConnection>(x => x.ActiveGrantId)
            .OnDelete(DeleteBehavior.Restrict);
        connection.HasOne(x => x.RevokedByUser)
            .WithMany()
            .HasForeignKey(x => x.RevokedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
