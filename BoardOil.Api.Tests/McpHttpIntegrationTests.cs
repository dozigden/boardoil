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

public sealed class McpHttpIntegrationTests : IAsyncLifetime
{
    private const string JwtIssuer = "boardoil";
    private const string JwtAudience = "boardoil";
    private const string JwtSigningKey = "replace-this-with-a-strong-32-char-min-signing-key";

    private string _databasePath = string.Empty;
    private BoardOilApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _databasePath = BuildDbPath("boardoil-api-mcp-tests");
        _factory = new BoardOilApiFactory(_databasePath);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task ToolsList_WithoutBearerToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await SendMcpRequestAsync(client, "tools/list", new { }, "missing-token");
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<JsonElement>>();

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(401, payload!.StatusCode);
        Assert.Contains("Missing bearer token", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(response.Headers.Contains("WWW-Authenticate"));
        Assert.Equal("Bearer", payload.Data.GetProperty("auth").GetProperty("scheme").GetString());
    }

    [Fact]
    public async Task ToolsList_WithInvalidBearerToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid-token");

        // Act
        var response = await SendMcpRequestAsync(client, "tools/list", new { }, "invalid-token");
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<JsonElement>>();

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(401, payload!.StatusCode);
        Assert.Contains("Invalid or expired bearer token", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(response.Headers.Contains("WWW-Authenticate"));
    }

    [Fact]
    public async Task ToolsList_WithExpiredBearerToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(DateTime.UtcNow.AddMinutes(-5)));

        // Act
        var response = await SendMcpRequestAsync(client, "tools/list", new { }, "expired-token");
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<JsonElement>>();

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(401, payload!.StatusCode);
        Assert.Contains("Invalid or expired bearer token", payload.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WellKnownMcp_ShouldReturnAuthAndEndpointMetadata()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/.well-known/mcp");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("BoardOil MCP", payload.RootElement.GetProperty("name").GetString());
        Assert.Equal("mcp-http", payload.RootElement.GetProperty("protocol").GetString());
        Assert.EndsWith("/mcp", payload.RootElement.GetProperty("endpoint").GetString(), StringComparison.Ordinal);
        Assert.Equal("Bearer", payload.RootElement.GetProperty("auth").GetProperty("scheme").GetString());
    }

    [Theory]
    [InlineData("/sse")]
    [InlineData("/messages")]
    [InlineData("/v1/mcp")]
    public async Task UnsupportedMcpStylePath_ShouldReturnJsonNotFound(string path)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(path);
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<JsonElement>>();

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(404, payload!.StatusCode);
        Assert.Equal(path, payload.Data.GetProperty("requestedPath").GetString());
        Assert.Contains("/mcp", payload.Data.GetProperty("endpoint").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ToolsAndMutations_WithValidBearerToken_ShouldSucceed()
    {
        // Arrange
        var client = _factory.CreateClient();
        await RegisterInitialAdminAsync(client);
        await LoginMachineAsync(client);

        // Act
        var toolsListResponse = await SendMcpRequestAsync(client, "tools/list", new { }, "tools-list");
        Assert.Equal(HttpStatusCode.OK, toolsListResponse.StatusCode);
        using var toolsListPayload = await ParseMcpJsonAsync(toolsListResponse);

        var boardGetResponse = await SendMcpRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "board.get",
                arguments = new { boardId = 1 }
            },
            "board-get");
        Assert.Equal(HttpStatusCode.OK, boardGetResponse.StatusCode);
        using var boardGetPayload = await ParseMcpJsonAsync(boardGetResponse);

        var todoColumnId = boardGetPayload.RootElement
            .GetProperty("result")
            .GetProperty("structuredContent")
            .GetProperty("data")
            .GetProperty("columns")
            .EnumerateArray()
            .Single(column => column.GetProperty("title").GetString() == "Todo")
            .GetProperty("columnId")
            .GetInt32();

        var createResponse = await SendMcpRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.create",
                arguments = new
                {
                    boardId = 1,
                    boardColumnId = todoColumnId,
                    title = "API in-process MCP test",
                    description = "",
                    tagNames = Array.Empty<string>()
                }
            },
            "card-create");
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var verifyResponse = await SendMcpRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "board.get",
                arguments = new { boardId = 1 }
            },
            "board-verify");
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        using var verifyPayload = await ParseMcpJsonAsync(verifyResponse);

        // Assert
        var toolNames = toolsListPayload.RootElement
            .GetProperty("result")
            .GetProperty("tools")
            .EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString())
            .ToArray();
        Assert.Contains("board.get", toolNames);
        Assert.Contains("card.create", toolNames);

        var cards = verifyPayload.RootElement
            .GetProperty("result")
            .GetProperty("structuredContent")
            .GetProperty("data")
            .GetProperty("columns")
            .EnumerateArray()
            .SelectMany(column => column.GetProperty("cards").EnumerateArray())
            .ToArray();
        Assert.Contains(cards, card => card.GetProperty("title").GetString() == "API in-process MCP test");
    }

    private static async Task RegisterInitialAdminAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register-initial-admin", new LoginRequest("admin", "Password1234!"));
        response.EnsureSuccessStatusCode();
    }

    private static async Task LoginMachineAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/machine/login", new LoginRequest("admin", "Password1234!"));
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<MachineSessionEnvelope>>();
        Assert.NotNull(payload);
        Assert.NotNull(payload!.Data);
        client.DefaultRequestHeaders.Authorization = new("Bearer", payload.Data!.AccessToken);
    }

    private static async Task<HttpResponseMessage> SendMcpRequestAsync(HttpClient client, string method, object @params, string id)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = method,
                ["params"] = @params
            })
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        return await client.SendAsync(request);
    }

    private static async Task<JsonDocument> ParseMcpJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var trimmed = content.TrimStart();

        if (trimmed.StartsWith('{'))
        {
            return JsonDocument.Parse(trimmed);
        }

        var sseJsonPayload = trimmed
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("data:", StringComparison.Ordinal))
            .Select(line => line["data:".Length..].Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .LastOrDefault();
        if (sseJsonPayload is not null)
        {
            return JsonDocument.Parse(sseJsonPayload);
        }

        throw new JsonException($"MCP response was not parseable JSON: {content}");
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

    private static string BuildDbPath(string dbNamePrefix)
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), ".test-data");
        Directory.CreateDirectory(root);
        return Path.Combine(root, $"{dbNamePrefix}-{Guid.NewGuid():N}.db");
    }

    private sealed record LoginRequest(string UserName, string Password);
    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
    private sealed record MachineSessionEnvelope(string AccessToken);
}
