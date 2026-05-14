using BoardOil.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoardOil.Ef.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<EntityUser>
{
    public void Configure(EntityTypeBuilder<EntityUser> user)
    {
        user.HasKey(x => x.Id);
        user.Property(x => x.UserName).HasMaxLength(64).IsRequired();
        user.Property(x => x.DisplayName).HasMaxLength(64).IsRequired();
        user.Property(x => x.Email).HasMaxLength(320).IsRequired();
        user.Property(x => x.NormalisedEmail).HasMaxLength(320).IsRequired();
        user.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        user.Property(x => x.Role).IsRequired();
        user.Property(x => x.IdentityType).IsRequired();
        user.Property(x => x.IsActive).IsRequired();
        user.ToTable("Users");
        user.HasIndex(x => x.UserName).IsUnique();
        user.HasIndex(x => x.NormalisedEmail).IsUnique();
        user.HasMany(x => x.RefreshTokens)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        user.HasMany(x => x.PersonalAccessTokens)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        user.HasMany(x => x.BoardMemberships)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        user.HasMany(x => x.CardComments)
            .WithOne(x => x.AuthorUser)
            .HasForeignKey(x => x.AuthorUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
