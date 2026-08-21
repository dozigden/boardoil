using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class OAuthTokenAuditConfiguration : IEntityTypeConfiguration<EntityOAuthTokenAudit>
{
    public void Configure(EntityTypeBuilder<EntityOAuthTokenAudit> audit)
    {
        audit.HasKey(x => x.Id);
        audit.Property(x => x.OccurredAtUtc).IsRequired();
        audit.Property(x => x.Outcome).HasMaxLength(16).IsRequired();
        audit.Property(x => x.GrantType).HasMaxLength(32).IsRequired();
        audit.Property(x => x.ErrorCode).HasMaxLength(64);
        audit.Property(x => x.ErrorDescription).HasMaxLength(2048);
        audit.Property(x => x.ErrorUri).HasMaxLength(2048);
        audit.Property(x => x.PresentedTokenId).HasMaxLength(100);
        audit.Property(x => x.PresentedTokenFingerprint).HasMaxLength(71);
        audit.Property(x => x.IssuedRefreshTokenFingerprint).HasMaxLength(71);
        audit.Property(x => x.AuthorizationId).HasMaxLength(100);
        audit.Property(x => x.Subject).HasMaxLength(400);
        audit.Property(x => x.OAuthClientId).HasMaxLength(100);
        audit.Property(x => x.OAuthConnectionName).HasMaxLength(120);
        audit.Property(x => x.OwnerUserName).HasMaxLength(64);
        audit.Property(x => x.OAuthClientDisplayName).HasMaxLength(200);
        audit.Property(x => x.Resource).HasMaxLength(2048);
        audit.Property(x => x.TraceIdentifier).HasMaxLength(128);
        audit.Property(x => x.UserAgent).HasMaxLength(2048);
        audit.Property(x => x.CreatedAtUtc).IsRequired();

        audit.HasIndex(x => x.OccurredAtUtc);
        audit.HasIndex(x => x.PresentedTokenId);
        audit.HasIndex(x => x.PresentedTokenFingerprint);
        audit.HasIndex(x => x.IssuedRefreshTokenFingerprint);
        audit.HasIndex(x => x.AuthorizationId);
        audit.HasIndex(x => x.OAuthConnectionId);
        audit.HasIndex(x => new { x.OAuthClientId, x.OccurredAtUtc });
        audit.HasIndex(x => new { x.Outcome, x.OccurredAtUtc });
        audit.ToTable("OAuthTokenAudits");
    }
}
