using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class PersonalAccessTokenConfiguration : IEntityTypeConfiguration<EntityPersonalAccessToken>
{
    public void Configure(EntityTypeBuilder<EntityPersonalAccessToken> personalAccessToken)
    {
        personalAccessToken.HasKey(x => x.Id);
        personalAccessToken.Property(x => x.Name).HasMaxLength(120).IsRequired();
        personalAccessToken.Property(x => x.TokenHash).HasMaxLength(200).IsRequired();
        personalAccessToken.Property(x => x.TokenPrefix).HasMaxLength(24).IsRequired();
        personalAccessToken.Property(x => x.ScopesCsv).HasMaxLength(500).IsRequired();
        personalAccessToken.Property(x => x.CreatedAtUtc).IsRequired();
        personalAccessToken.Property(x => x.ExpiresAtUtc).IsRequired(false);
        personalAccessToken.Property(x => x.LastUsedAtUtc).IsRequired(false);
        personalAccessToken.Property(x => x.RevokedAtUtc).IsRequired(false);
        personalAccessToken.ToTable("PersonalAccessTokens");
        personalAccessToken.HasIndex(x => x.TokenHash).IsUnique();
        personalAccessToken.HasIndex(x => x.UserId);
    }
}
