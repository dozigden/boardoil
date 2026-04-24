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
    public async Task AdminUser_GetSystemUsers_ShouldExcludeClientAccounts()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await CreateClientAccountAsAdminAsync(adminClient, "client-bot", "Standard");

        // Act
        var response = await adminClient.GetAsync("/api/system/users");
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<IReadOnlyList<ManagedUserEnvelope>>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        Assert.Contains(envelope.Data!, x => x.UserName == "member");
        Assert.DoesNotContain(envelope.Data!, x => x.UserName == "client-bot");
    }

    [Fact]
    public async Task AdminUser_CreateUser_WithDuplicateEmail_ShouldReturnBadRequest()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        _ = await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");

        // Act
        var response = await adminClient.PostAsJsonAsync(
            "/api/system/users",
            new CreateUserRequest("member-two", "MEMBER@LOCALHOST", "Password1234!", "Standard"));
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.Equal("Email already exists.", envelope!.Message);
    }

    [Fact]
    public async Task AdminUser_UpdateUser_ShouldUpdateEmailRoleAndStatus()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var memberUserId = await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");

        // Act
        var response = await adminClient.PutAsJsonAsync(
            $"/api/system/users/{memberUserId}",
            new UpdateUserRequest("member-updated@localhost", "Admin", false));
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<BoardOil.Contracts.Users.ManagedUserDto>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        Assert.Equal("member-updated@localhost", envelope.Data!.Email);
        Assert.Equal("Admin", envelope.Data.Role);
        Assert.False(envelope.Data.IsActive);
    }

    [Fact]
    public async Task AdminUser_CreateClientAccount_WithDuplicateEmail_ShouldReturnBadRequest()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateClientAccountAsAdminAsync(adminClient, "client-one", "Standard");

        // Act
        var response = await adminClient.PostAsJsonAsync(
            "/api/system/client-accounts",
            new CreateClientAccountRequest("client-two", "CLIENT-ONE@LOCALHOST", "Standard"));
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.Equal("Email already exists.", envelope!.Message);
    }

    [Fact]
    public async Task AdminUser_UpdateClientAccount_ShouldUpdateEmailRoleAndStatus()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateClientAccountAsAdminAsync(adminClient, "client-bot", "Standard");
        var listResponse = await adminClient.GetAsync("/api/system/client-accounts");
        var listEnvelope = await listResponse.Content.ReadFromJsonAsync<ApiEnvelope<IReadOnlyList<ClientAccountEnvelope>>>();
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(listEnvelope);
        Assert.NotNull(listEnvelope!.Data);
        var clientId = Assert.Single(listEnvelope.Data!, x => x.UserName == "client-bot").Id;

        // Act
        var updateResponse = await adminClient.PutAsJsonAsync(
            $"/api/system/client-accounts/{clientId}",
            new UpdateClientAccountRequest("client-bot-updated@localhost", "Admin", false));
        var updateEnvelope = await updateResponse.Content.ReadFromJsonAsync<ApiEnvelope<ClientAccountEnvelope>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updateEnvelope);
        Assert.NotNull(updateEnvelope!.Data);
        Assert.Equal("client-bot-updated@localhost", updateEnvelope.Data!.Email);
        Assert.Equal("Admin", updateEnvelope.Data.Role);
        Assert.False(updateEnvelope.Data.IsActive);
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
    public async Task AdminUser_ResetUserPassword_ShouldAllowLoginWithNewPasswordOnly()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        var memberClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var memberUserId = await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await LoginAsAsync(memberClient, "member", "Password1234!");

        // Act
        var resetResponse = await adminClient.PutAsJsonAsync(
            $"/api/system/users/{memberUserId}/password",
            new ResetUserPasswordRequest("FreshPassword1234!"));
        var oldLoginResponse = await memberClient.PostAsJsonAsync("/api/auth/login", new LoginRequest("member", "Password1234!"));
        var newLoginResponse = await memberClient.PostAsJsonAsync("/api/auth/login", new LoginRequest("member", "FreshPassword1234!"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLoginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newLoginResponse.StatusCode);
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

    [Fact]
    public async Task AdminUser_ResetUserPassword_WithInvalidNewPassword_ShouldReturnBadRequest()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var memberUserId = await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");

        // Act
        var response = await adminClient.PutAsJsonAsync(
            $"/api/system/users/{memberUserId}/password",
            new ResetUserPasswordRequest("short"));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
