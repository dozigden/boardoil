using System.Net;
using System.Net.Http.Json;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Mcp;
using BoardOil.Contracts.Users;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpProjectConnectionApiIntegrationTests : AuthAuthorisationIntegrationTestBase
{
    [Fact]
    public async Task CreateConnection_WhenAdmin_ShouldReturnCreatedConnectionInList()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var clientAccountId = await CreateClientAccountAsync(adminClient, "repository-client");

        // Act
        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/system/mcp-project-connections",
            new CreateMcpProjectConnectionRequest(
                clientAccountId,
                "Repository connection",
                [MachinePatScopes.McpRead, MachinePatScopes.McpWrite]));
        var createEnvelope = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<McpProjectConnectionDto>>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createEnvelope);
        Assert.NotNull(createEnvelope!.Data);
        Assert.Equal(clientAccountId, createEnvelope.Data!.ClientAccountId);
        Assert.Equal("repository-client", createEnvelope.Data.ClientAccountUserName);
        Assert.Matches("^[0-9a-f]{64}$", createEnvelope.Data.PublicId);
        Assert.Equal($"/mcp/connections/{createEnvelope.Data.PublicId}", createEnvelope.Data.ResourceUrl);
        Assert.True(createEnvelope.Data.IsActive);
        Assert.Equal("admin", createEnvelope.Data.CreatedByUserName);

        var listEnvelope = await adminClient.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<McpProjectConnectionDto>>>(
            "/api/system/mcp-project-connections");
        Assert.NotNull(listEnvelope);
        var listed = Assert.Single(listEnvelope!.Data!);
        Assert.Equal(createEnvelope.Data.Id, listed.Id);
    }

    [Fact]
    public async Task RevokeConnection_WhenAdmin_ShouldRetainRevokedConnection()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var clientAccountId = await CreateClientAccountAsync(adminClient, "repository-client");
        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/system/mcp-project-connections",
            new CreateMcpProjectConnectionRequest(
                clientAccountId,
                "Repository connection",
                [MachinePatScopes.McpRead]));
        var createEnvelope = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<McpProjectConnectionDto>>();
        Assert.NotNull(createEnvelope?.Data);

        // Act
        var revokeResponse = await adminClient.DeleteAsync(
            $"/api/system/mcp-project-connections/{createEnvelope!.Data!.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
        var listEnvelope = await adminClient.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<McpProjectConnectionDto>>>(
            "/api/system/mcp-project-connections");
        Assert.NotNull(listEnvelope);
        var revoked = Assert.Single(listEnvelope!.Data!);
        Assert.Equal(createEnvelope.Data.Id, revoked.Id);
        Assert.False(revoked.IsActive);
        Assert.NotNull(revoked.RevokedAtUtc);
        Assert.Equal("admin", revoked.RevokedByUserName);
    }

    [Theory]
    [InlineData("GET", "/api/system/mcp-project-connections")]
    [InlineData("POST", "/api/system/mcp-project-connections")]
    [InlineData("DELETE", "/api/system/mcp-project-connections/1")]
    public async Task ProjectConnectionEndpoints_WhenStandardUser_ShouldReturnForbidden(string method, string path)
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        var standardClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await LoginAsAsync(standardClient, "member", "Password1234!");
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
        {
            request.Content = JsonContent.Create(
                new CreateMcpProjectConnectionRequest(1, "Repository", [MachinePatScopes.McpRead]));
        }

        // Act
        var response = await standardClient.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<int> CreateClientAccountAsync(HttpClient adminClient, string userName)
    {
        var response = await adminClient.PostAsJsonAsync(
            "/api/system/client-accounts",
            new BoardOil.Contracts.Users.CreateClientAccountRequest(
                userName,
                "Repository Client",
                $"{userName}@localhost",
                "Standard"));
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CreatedClientAccountDto>>();
        Assert.NotNull(envelope?.Data);
        return envelope!.Data!.Account.Id;
    }
}
