using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpHttpAuthAndPathIntegrationTests : McpIntegrationTestBase
{
    private const string JwtIssuer = "boardoil";
    private const string JwtAudience = "boardoil";
    private const string JwtSigningKey = "replace-this-with-a-strong-32-char-min-signing-key";

    [Fact]
    public async Task ToolsList_WithoutBearerToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/list", new { }, "missing-token");
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<JsonElement>>();

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(401, payload!.StatusCode);
        Assert.Contains("Missing bearer token", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(response.Headers.Contains("WWW-Authenticate"));
        Assert.Equal("Bearer", payload.Data.GetProperty("auth").GetProperty("scheme").GetString());
        Assert.Equal("personal_access_token", payload.Data.GetProperty("setup").GetProperty("preferredAuth").GetString());
        Assert.Equal("/access-tokens", payload.Data.GetProperty("setup").GetProperty("patManagementUi").GetString());
        Assert.Equal("POST", payload.Data.GetProperty("examples").GetProperty("toolsListRequest").GetProperty("method").GetString());
    }

    [Fact]
    public async Task ToolsList_WithInvalidBearerToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid-token");

        // Act
        var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/list", new { }, "invalid-token");
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<JsonElement>>();

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(401, payload!.StatusCode);
        Assert.Contains("Invalid or expired bearer token", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(response.Headers.Contains("WWW-Authenticate"));
        Assert.Equal("POST", payload.Data.GetProperty("examples").GetProperty("toolsListRequest").GetProperty("method").GetString());
    }

    [Fact]
    public async Task ToolsList_WithMachineJwtBearer_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var accessToken = await LoginMachineAsync(client);
        client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);

        // Act
        var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/list", new { }, "machine-jwt");
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<JsonElement>>();

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(401, payload!.StatusCode);
        Assert.Contains("Invalid or expired bearer token", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(response.Headers.Contains("WWW-Authenticate"));
        Assert.Equal("Bearer", payload.Data.GetProperty("auth").GetProperty("scheme").GetString());
        Assert.Equal("personal_access_token", payload.Data.GetProperty("setup").GetProperty("preferredAuth").GetString());
        Assert.Equal("POST", payload.Data.GetProperty("examples").GetProperty("toolsListRequest").GetProperty("method").GetString());
    }

    [Fact]
    public async Task ToolsList_WithExpiredBearerToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(DateTime.UtcNow.AddMinutes(-5)));

        // Act
        var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/list", new { }, "expired-token");
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<JsonElement>>();

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(401, payload!.StatusCode);
        Assert.Contains("Invalid or expired bearer token", payload.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/sse")]
    [InlineData("/sse/stream")]
    [InlineData("/messages")]
    [InlineData("/messages/subscribe")]
    [InlineData("/v1/mcp")]
    [InlineData("/v1/mcp/initialize")]
    public async Task UnsupportedMcpStylePath_ShouldReturnJsonNotFound(string path)
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync(path);
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<JsonElement>>();

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(404, payload!.StatusCode);
        Assert.Equal(path, payload.Data.GetProperty("requestedPath").GetString());
        Assert.Equal("/mcp", payload.Data.GetProperty("endpoint").GetString());
        Assert.Equal("personal_access_token", payload.Data.GetProperty("setup").GetProperty("preferredAuth").GetString());
        Assert.Equal("POST", payload.Data.GetProperty("examples").GetProperty("toolsListRequest").GetProperty("method").GetString());
        Assert.Contains("PAT bearer token", payload.Data.GetProperty("nextStep").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateToken(DateTime expiresAtUtc)
    {
        var handler = new JwtSecurityTokenHandler();
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Name, "expired-user"),
                new Claim(ClaimTypes.Role, "Admin")
            ],
            notBefore: expiresAtUtc.AddMinutes(-10),
            expires: expiresAtUtc,
            signingCredentials: credentials);
        return handler.WriteToken(token);
    }
}
