using BoardOil.Abstractions.Users;
using BoardOil.Contracts.Users;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Tests.Infrastructure;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class UserServiceTests : TestBaseDb
{
    [Fact]
    public async Task GetCurrentIdentityAsync_WhenActorIsActiveClientAccount_ShouldReturnPublicIdentity()
    {
        // Arrange
        var client = new EntityUser
        {
            UserName = "agent-client",
            DisplayName = "Agent Client",
            Email = "agent-client@localhost",
            Role = UserRole.Standard,
            IdentityType = UserIdentityType.Client,
            IsActive = true,
        };
        DbContextForArrange.Users.Add(client);
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<IUserService>();

        // Act
        var result = await service.GetCurrentIdentityAsync(client.Id);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(client.Id, result.Data!.Id);
        Assert.Equal("agent-client", result.Data.UserName);
        Assert.Equal("Agent Client", result.Data.DisplayName);
        Assert.Equal("Standard", result.Data.Role);
    }

    [Fact]
    public async Task GetCurrentIdentityAsync_WhenActorIsInactive_ShouldReturnUnauthorized()
    {
        // Arrange
        var inactive = new EntityUser
        {
            UserName = "inactive-agent",
            DisplayName = "Inactive Agent",
            Email = "inactive-agent@localhost",
            Role = UserRole.Standard,
            IsActive = false,
        };
        DbContextForArrange.Users.Add(inactive);
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<IUserService>();

        // Act
        var result = await service.GetCurrentIdentityAsync(inactive.Id);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task GetUsersAsync_ShouldReturnUsersIncludingClientAccountsInUserNameOrder()
    {
        // Arrange
        var now = DateTime.UtcNow;
        DbContextForArrange.Users.AddRange(
            new EntityUser
            {
                UserName = "zz-member",
                DisplayName = "zz-member",
                Email = "zz-member@localhost",
                PasswordHash = "hash",
                Role = UserRole.Standard,
                IdentityType = UserIdentityType.User,
                IsActive = true,
            },
            new EntityUser
            {
                UserName = "aa-client",
                DisplayName = "aa-client",
                Email = "aa-client@localhost",
                PasswordHash = "hash",
                Role = UserRole.Standard,
                IdentityType = UserIdentityType.Client,
                IsActive = true,
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

    [Fact]
    public async Task UpdateOwnProfileAsync_WithDuplicateEmail_ShouldReturnEmailFieldErrorWithoutSaving()
    {
        // Arrange
        DbContextForArrange.Users.Add(new EntityUser
        {
            UserName = "other-user",
            DisplayName = "Other User",
            Email = "other@localhost",
            PasswordHash = "hash",
            Role = UserRole.Standard,
            IdentityType = UserIdentityType.User,
            IsActive = true,
        });
        await DbContextForArrange.SaveChangesAsync();
        var original = DbContextForAssert.Users.Where(x => x.Id == ActorUserId)
            .Select(x => new { x.DisplayName, x.Email }).Single();
        var service = ResolveService<IUserService>();

        // Act
        var result = await service.UpdateOwnProfileAsync(ActorUserId, new UpdateOwnUserProfileRequest("Changed Name", " OTHER@localhost ", true));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(["Email already exists."], result.ValidationErrors!["email"]);
        var stored = DbContextForAssert.Users.Single(x => x.Id == ActorUserId);
        Assert.Equal(original.DisplayName, stored.DisplayName);
        Assert.Equal(original.Email, stored.Email);
        Assert.False(stored.ExperimentalFeaturesEnabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateOwnProfileAsync_WithValidValues_ShouldPersistProfileAndExperimentalOptIn(bool experimentalFeaturesEnabled)
    {
        var actor = DbContextForArrange.Users.Single(x => x.Id == ActorUserId);
        actor.ExperimentalFeaturesEnabled = !experimentalFeaturesEnabled;
        var other = new EntityUser
        {
            UserName = "other-user",
            DisplayName = "Other User",
            Email = "other@localhost",
            ExperimentalFeaturesEnabled = !experimentalFeaturesEnabled
        };
        DbContextForArrange.Users.Add(other);
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<IUserService>();

        var result = await service.UpdateOwnProfileAsync(ActorUserId,
            new UpdateOwnUserProfileRequest("Actor Updated", "actor-updated@localhost", experimentalFeaturesEnabled));

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Actor Updated", result.Data!.DisplayName);
        Assert.Equal("actor-updated@localhost", result.Data.Email);
        Assert.Equal(experimentalFeaturesEnabled, result.Data.ExperimentalFeaturesEnabled);

        var storedUser = DbContextForAssert.Users.Single(x => x.Id == ActorUserId);
        Assert.Equal("Actor Updated", storedUser.DisplayName);
        Assert.Equal("actor-updated@localhost", storedUser.Email);
        Assert.Equal(experimentalFeaturesEnabled, storedUser.ExperimentalFeaturesEnabled);
        Assert.Equal(!experimentalFeaturesEnabled, DbContextForAssert.Users.Single(x => x.Id == other.Id).ExperimentalFeaturesEnabled);
        var profile = await service.GetOwnProfileAsync(ActorUserId);
        Assert.Equal(experimentalFeaturesEnabled, profile.Data!.ExperimentalFeaturesEnabled);
        var identity = await service.GetCurrentIdentityAsync(ActorUserId);
        Assert.Equal(experimentalFeaturesEnabled, identity.Data!.ExperimentalFeaturesEnabled);
    }
}
