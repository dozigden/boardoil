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
    public async Task StandardUser_GetAdminUsers_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        var standardClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.GetAsync("/api/system/users");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_ResetUserPassword_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        var standardClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var memberUserId = await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.PutAsJsonAsync(
            $"/api/system/users/{memberUserId}/password",
            new ResetUserPasswordRequest("FreshPassword1234!"));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

}
