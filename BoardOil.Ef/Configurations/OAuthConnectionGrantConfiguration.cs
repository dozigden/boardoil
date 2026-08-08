using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class OAuthConnectionGrantConfiguration : IEntityTypeConfiguration<EntityOAuthConnectionGrant>
{
    public void Configure(EntityTypeBuilder<EntityOAuthConnectionGrant> grant)
    {
        grant.HasKey(x => x.Id);
        grant.Property(x => x.OpenIddictApplicationId).HasMaxLength(100).IsRequired();
        grant.Property(x => x.OpenIddictAuthorizationId).HasMaxLength(100).IsRequired();
        grant.Property(x => x.OAuthClientId).HasMaxLength(100).IsRequired();
        grant.Property(x => x.OAuthClientDisplayName).HasMaxLength(120).IsRequired();
        grant.Property(x => x.Resource).HasMaxLength(2048).IsRequired();
        grant.Property(x => x.ApprovedScopesCsv).HasMaxLength(100).IsRequired();
        grant.Property(x => x.RevokedByUserName).HasMaxLength(64).IsRequired(false);
        grant.Property(x => x.RevocationReason).HasMaxLength(64).IsRequired(false);
        grant.Property(x => x.ApprovedAtUtc).IsRequired();
        grant.Property(x => x.CreatedAtUtc).IsRequired();
        grant.Property(x => x.UpdatedAtUtc).IsRequired();
        grant.Property(x => x.RevokedAtUtc).IsRequired(false);
        grant.ToTable("OAuthConnectionGrants");

        grant.HasIndex(x => x.OpenIddictAuthorizationId).IsUnique();
        grant.HasIndex(x => x.OpenIddictApplicationId);
        grant.HasIndex(x => x.OAuthConnectionId);
        grant.HasIndex(x => x.RevokedAtUtc);

        grant.HasOne(x => x.OAuthConnection)
            .WithMany(x => x.Grants)
            .HasForeignKey(x => x.OAuthConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
        grant.HasOne(x => x.RevokedByUser)
            .WithMany()
            .HasForeignKey(x => x.RevokedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
