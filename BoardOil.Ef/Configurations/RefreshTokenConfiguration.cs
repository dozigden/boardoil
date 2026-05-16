using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<EntityRefreshToken>
{
    public void Configure(EntityTypeBuilder<EntityRefreshToken> refreshToken)
    {
        refreshToken.HasKey(x => x.Id);
        refreshToken.Property(x => x.TokenHash).HasMaxLength(200).IsRequired();
        refreshToken.Property(x => x.ExpiresAtUtc).IsRequired();
        refreshToken.Property(x => x.CreatedAtUtc).IsRequired();
        refreshToken.Property(x => x.RevokedAtUtc).IsRequired(false);
        refreshToken.Property(x => x.ReplacedByTokenHash).HasMaxLength(200).IsRequired(false);
        refreshToken.ToTable("RefreshTokens");
        refreshToken.HasIndex(x => x.TokenHash).IsUnique();
    }
}
