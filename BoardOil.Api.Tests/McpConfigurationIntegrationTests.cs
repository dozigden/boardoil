using System.Net;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpNoAuthConfigurationIntegrationTests : McpIntegrationTestBase
{
    [Fact]
    public async Task ToolsList_WithoutBearerToken_WhenAuthModeNone_ShouldReturnOk()
    {
        var client = CreateClient();

        var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/list", new { }, "missing-token-no-auth");
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(payload.RootElement.TryGetProperty("result", out var result));
        Assert.True(result.TryGetProperty("tools", out _));
    }

    [Fact]
    public async Task WellKnownMcp_WhenAuthModeNone_ShouldAdvertiseNoAuth()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/.well-known/mcp");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("None", payload.RootElement.GetProperty("auth").GetProperty("scheme").GetString());
        Assert.Equal("none", payload.RootElement.GetProperty("setup").GetProperty("preferredAuth").GetString());
        Assert.False(payload.RootElement.GetProperty("examples").GetProperty("genericMcpConfig").TryGetProperty("headers", out _));
    }

    protected override BoardOilApiFactory CreateFactory(string databasePath) =>
        new(
            databasePath,
            configurationOverrides: new Dictionary<string, string?>
            {
                ["BoardOilMcp:AuthMode"] = "none"
            });
}

public sealed class McpLegacySseConfigurationIntegrationTests : McpIntegrationTestBase
{
    [Fact]
    public async Task SsePath_WhenLegacySseEnabled_ShouldReturnAuthErrorInsteadOfUnsupportedPath()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/sse");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(401, payload.RootElement.GetProperty("statusCode").GetInt32());
        Assert.Contains(
            "Missing bearer token",
            payload.RootElement.GetProperty("message").GetString(),
            StringComparison.OrdinalIgnoreCase);
    }

    protected override BoardOilApiFactory CreateFactory(string databasePath) =>
        new(
            databasePath,
            configurationOverrides: new Dictionary<string, string?>
            {
                ["BoardOilMcp:TransportMode"] = "both"
            });
}
