using System.Net;
using System.Net.Http.Json;
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
    public async Task IdentityGet_WithoutBearerToken_WhenAuthModeNone_ShouldReturnConfiguredActor()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);

        // Act
        var response = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new { name = "identity_get", arguments = new { } },
            "identity-get-no-auth");
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var identity = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal("admin", identity.GetProperty("user").GetProperty("userName").GetString());
        Assert.Equal("None", identity.GetProperty("authentication").GetProperty("type").GetString());
        Assert.Empty(identity.GetProperty("authentication").GetProperty("scopes").EnumerateArray());
    }

    [Fact]
    public async Task LegacyRequests_WhenAuthModeNone_ShouldRemainSessionless()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var initializeResponse = await McpJsonRpcClient.SendLegacyInitializeAsync(
            client,
            "legacy-no-auth-initialize");
        var toolsResponse = await McpJsonRpcClient.SendLegacyRequestAsync(
            client,
            "tools/list",
            new { },
            "legacy-no-auth-tools");

        // Assert
        Assert.Equal(HttpStatusCode.OK, initializeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, toolsResponse.StatusCode);
        Assert.False(initializeResponse.Headers.Contains("Mcp-Session-Id"));
        Assert.False(toolsResponse.Headers.Contains("Mcp-Session-Id"));
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

        var response = await client.GetAsync("/mcp/sse");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(401, payload.RootElement.GetProperty("statusCode").GetInt32());
        Assert.Contains(
            "Missing bearer token",
            payload.RootElement.GetProperty("message").GetString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MessagePath_WhenLegacySseEnabled_ShouldReturnAuthErrorInsteadOfUnsupportedPath()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.PostAsync("/mcp/message", JsonContent.Create(new { }));
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(401, payload.RootElement.GetProperty("statusCode").GetInt32());
        Assert.Contains(
            "Missing bearer token",
            payload.RootElement.GetProperty("message").GetString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SsePath_WithPatWhenLegacySseEnabled_ShouldCompleteToolsListRoundTrip()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var sseClient = await LegacySseTestClient.ConnectAsync(
            client,
            "/mcp/sse",
            patToken,
            cancellationSource.Token);

        // Act
        const string initializeId = "legacy-sse-initialize";
        using var initializePostResponse = await sseClient.SendMessageAsync(
            new
            {
                jsonrpc = "2.0",
                id = initializeId,
                method = "initialize",
                @params = new
                {
                    protocolVersion = McpJsonRpcClient.LegacyProtocolVersion,
                    capabilities = new { },
                    clientInfo = new
                    {
                        name = "BoardOil legacy SSE integration tests",
                        version = "1.0.0"
                    }
                }
            },
            cancellationSource.Token);
        using var initializePayload = await sseClient.ReadResponseAsync(
            initializeId,
            cancellationSource.Token);
        using var initializedPostResponse = await sseClient.SendMessageAsync(
            new
            {
                jsonrpc = "2.0",
                method = "notifications/initialized",
                @params = new { }
            },
            cancellationSource.Token);
        const string toolsListId = "legacy-sse-tools";
        using var toolsListPostResponse = await sseClient.SendMessageAsync(
            new
            {
                jsonrpc = "2.0",
                id = toolsListId,
                method = "tools/list",
                @params = new { }
            },
            cancellationSource.Token);
        using var toolsListPayload = await sseClient.ReadResponseAsync(
            toolsListId,
            cancellationSource.Token);

        // Assert
        Assert.Equal("text/event-stream", sseClient.MediaType);
        Assert.Equal("/mcp/message", sseClient.MessageEndpoint.AbsolutePath);
        Assert.Equal(HttpStatusCode.Accepted, initializePostResponse.StatusCode);
        Assert.Equal(
            McpJsonRpcClient.LegacyProtocolVersion,
            initializePayload.RootElement.GetProperty("result").GetProperty("protocolVersion").GetString());
        Assert.Equal(HttpStatusCode.Accepted, initializedPostResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, toolsListPostResponse.StatusCode);
        Assert.NotEmpty(toolsListPayload.RootElement
            .GetProperty("result")
            .GetProperty("tools")
            .EnumerateArray());
    }

    [Fact]
    public async Task LegacyInitialize_WhenLegacySseEnabled_ShouldCreateStatefulSession()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);

        // Act
        var initializeResponse = await McpJsonRpcClient.SendLegacyInitializeAsync(
            client,
            "hybrid-legacy-initialize",
            patToken);
        var sessionId = initializeResponse.Headers.GetValues("Mcp-Session-Id").Single();
        var toolsResponse = await McpJsonRpcClient.SendLegacyRequestAsync(
            client,
            "tools/list",
            new { },
            "hybrid-legacy-tools",
            patToken,
            sessionId: sessionId);

        // Assert
        Assert.Equal(HttpStatusCode.OK, initializeResponse.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(sessionId));
        Assert.Equal(HttpStatusCode.OK, toolsResponse.StatusCode);
        Assert.Equal(sessionId, toolsResponse.Headers.GetValues("Mcp-Session-Id").Single());
    }

    [Fact]
    public async Task ServerDiscover_WhenLegacySseEnabled_ShouldRemainSessionless()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);

        // Act
        var response = await McpJsonRpcClient.SendRequestAsync(
            client,
            "server/discover",
            new { },
            "hybrid-modern-discover",
            patToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Mcp-Session-Id"));
    }

    protected override BoardOilApiFactory CreateFactory(string databasePath) =>
        new(
            databasePath,
            configurationOverrides: new Dictionary<string, string?>
            {
                ["BoardOilMcp:TransportMode"] = "both"
            });
}
