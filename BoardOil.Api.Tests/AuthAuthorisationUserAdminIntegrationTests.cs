using System.Net;
using System.Net.Http.Json;
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
            new UpdateOwnUserProfileRequest("Member Updated", "member-updated@localhost"));
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

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.NotNull(meEnvelope);
        Assert.NotNull(meEnvelope!.Data);
        Assert.Equal("Member Updated", meEnvelope.Data!.DisplayName);
    }

    private sealed record UpdateOwnUserProfileRequest(string DisplayName, string Email);
    private sealed record OwnUserProfileEnvelope(int Id, string UserName, string DisplayName, string Email, string Role);
    private sealed record AuthUserEnvelope(int Id, string UserName, string DisplayName, string Role);
}
