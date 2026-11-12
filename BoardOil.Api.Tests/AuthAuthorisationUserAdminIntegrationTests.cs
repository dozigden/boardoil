using System.Net;
using System.Net.Http.Json;
using BoardOil.Contracts.Auth;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class AuthAuthorisationUserAdminIntegrationTests : AuthAuthorisationIntegrationTestBase
{
    [Fact]
    public async Task StandardUser_GetUsers_ShouldIncludeClientAccounts()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        var standardClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await CreateClientAccountAsAdminAsync(adminClient, "client-bot", "Standard");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.GetAsync("/api/users");
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<IReadOnlyList<UserDirectoryEntryEnvelope>>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        Assert.Contains(envelope.Data!, x => x.UserName == "member");
        Assert.Contains(envelope.Data!, x => x.UserName == "client-bot");
    }

    [Fact]
    public async Task StandardUser_UpdateOwnProfile_ShouldReturnUpdatedProfile()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        var standardClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var updateResponse = await standardClient.PutAsJsonAsync(
            "/api/users/me",
            new UpdateOwnUserProfileRequest("Member Updated", "member-updated@localhost", true));
        var updateEnvelope = await updateResponse.Content.ReadFromJsonAsync<ApiEnvelope<OwnUserProfileEnvelope>>();

        var meResponse = await standardClient.GetAsync("/api/auth/me");
        var meEnvelope = await meResponse.Content.ReadFromJsonAsync<ApiEnvelope<AuthUserEnvelope>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updateEnvelope);
        Assert.NotNull(updateEnvelope!.Data);
        Assert.Equal("member", updateEnvelope.Data!.UserName);
        Assert.Equal("Member Updated", updateEnvelope.Data.DisplayName);
        Assert.Equal("member-updated@localhost", updateEnvelope.Data.Email);
        Assert.True(updateEnvelope.Data.ExperimentalFeaturesEnabled);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.NotNull(meEnvelope);
        Assert.NotNull(meEnvelope!.Data);
        Assert.Equal("Member Updated", meEnvelope.Data!.DisplayName);
        Assert.True(meEnvelope.Data.ExperimentalFeaturesEnabled);

        var refreshResponse = await standardClient.PostAsync("/api/auth/refresh", null);
        refreshResponse.EnsureSuccessStatusCode();
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<ApiEnvelope<AuthSessionDto>>();
        Assert.True(refreshed!.Data!.User.ExperimentalFeaturesEnabled);

        var loginResponse = await standardClient.PostAsJsonAsync("/api/auth/login", new LoginRequest("member", "Password1234!"));
        loginResponse.EnsureSuccessStatusCode();
        var loggedIn = await loginResponse.Content.ReadFromJsonAsync<ApiEnvelope<AuthSessionDto>>();
        Assert.True(loggedIn!.Data!.User.ExperimentalFeaturesEnabled);
    }

    [Theory]
    [InlineData("""{"displayName":"Changed","email":"changed@localhost"}""")]
    [InlineData("""{"displayName":"Changed","email":"changed@localhost","experimentalFeaturesEnabled":null}""")]
    public async Task UpdateOwnProfile_WithoutExplicitBoolean_ShouldRejectWithoutChangingProfile(string json)
    {
        var client = Factory.CreateClient();
        await RegisterInitialAdminAsync(client);
        var savedResponse = await client.PutAsJsonAsync("/api/users/me",
            new UpdateOwnUserProfileRequest("Original", "original@localhost", true));
        savedResponse.EnsureSuccessStatusCode();
        var saved = await savedResponse.Content.ReadFromJsonAsync<ApiEnvelope<OwnUserProfileEnvelope>>();

        var response = await client.PutAsync("/api/users/me",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var current = await client.GetFromJsonAsync<ApiEnvelope<OwnUserProfileEnvelope>>("/api/users/me");
        Assert.Equal(saved!.Data, current!.Data);
        Assert.True(current.Data!.ExperimentalFeaturesEnabled);
    }

    [Fact]
    public async Task UpdateOwnProfile_WithExplicitFalse_ShouldDisableExperimentalFeatures()
    {
        var client = Factory.CreateClient();
        await RegisterInitialAdminAsync(client);
        var enabled = await client.PutAsJsonAsync("/api/users/me",
            new UpdateOwnUserProfileRequest("Member", "member@localhost", true));
        enabled.EnsureSuccessStatusCode();

        var response = await client.PutAsJsonAsync("/api/users/me",
            new UpdateOwnUserProfileRequest("Member", "member@localhost", false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await response.Content.ReadFromJsonAsync<ApiEnvelope<OwnUserProfileEnvelope>>();
        Assert.False(saved!.Data!.ExperimentalFeaturesEnabled);
    }

    private sealed record UpdateOwnUserProfileRequest(string DisplayName, string Email, bool ExperimentalFeaturesEnabled);
    private sealed record OwnUserProfileEnvelope(int Id, string UserName, string DisplayName, string Email, string Role, bool ExperimentalFeaturesEnabled);
    private sealed record AuthUserEnvelope(int Id, string UserName, string DisplayName, string Role, bool ExperimentalFeaturesEnabled);
}
