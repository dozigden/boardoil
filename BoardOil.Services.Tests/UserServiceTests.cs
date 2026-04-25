using BoardOil.Abstractions.Users;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Services.Tests.Infrastructure;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class UserServiceTests : TestBaseDb
{
    [Fact]
    public async Task GetUsersAsync_ShouldReturnUsersIncludingClientAccountsInUserNameOrder()
    {
        // Arrange
        var now = DateTime.UtcNow;
        DbContextForArrange.Users.AddRange(
            new EntityUser
            {
                UserName = "zz-member",
                Email = "zz-member@localhost",
                NormalisedEmail = "zz-member@localhost",
                PasswordHash = "hash",
                Role = UserRole.Standard,
                IdentityType = UserIdentityType.User,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new EntityUser
            {
                UserName = "aa-client",
                Email = "aa-client@localhost",
                NormalisedEmail = "aa-client@localhost",
                PasswordHash = "hash",
                Role = UserRole.Standard,
                IdentityType = UserIdentityType.Client,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<IUserService>();

        // Act
        var result = await service.GetUsersAsync();

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var names = result.Data!.Select(x => x.UserName).ToArray();
        Assert.Equal(["aa-client", "actor", "zz-member"], names);
    }
}
