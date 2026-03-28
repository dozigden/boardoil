using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Server.Tests.Infrastructure;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace BoardOil.Mcp.Server.Tests;

public sealed class McpHttpIntegrationTests : IAsyncLifetime
{
    private const string TokenIssuer = "boardoil-test";
    private const string TokenAudience = "boardoil-test";
    private const string TokenSigningKey = "boardoil-test-signing-key-change-me-1234567890";

    private string _databasePath = string.Empty;
    private BoardOilMcpFactory _factory = null!;

    public Task InitializeAsync()
    {
        _databasePath = BuildDbPath("boardoil-mcp-http-tests");
        _factory = new BoardOilMcpFactory(_databasePath);
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
        var response = await SendJsonRpcAsync(client, "tools/list", new { }, "no-auth");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ToolsList_WithInvalidBearerToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid-token");

        // Act
        var response = await SendJsonRpcAsync(client, "tools/list", new { }, "invalid-auth");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ToolsList_WithExpiredBearerToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(DateTime.UtcNow.AddMinutes(-5)));

        // Act
        var response = await SendJsonRpcAsync(client, "tools/list", new { }, "expired-auth");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ToolsAndMutations_WithValidBearerToken_ShouldSucceed()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(DateTime.UtcNow.AddMinutes(10)));

        // Act
        var toolsListResponse = await SendJsonRpcAsync(client, "tools/list", new { }, "tools-list");
        var initialBoardGetResponse = await SendJsonRpcAsync(
            client,
            "tools/call",
            new
            {
                name = ToolNames.BoardGet,
                arguments = new { boardId = 1 }
            },
            "board-get-initial");
        var createCardResponse = await SendJsonRpcAsync(
            client,
            "tools/call",
            new
            {
                name = ToolNames.CardCreate,
                arguments = new
                {
                    boardId = 1,
                    boardColumnId = 1,
                    title = "MCP smoke card",
                    description = "Created through authenticated MCP HTTP",
                    tagNames = Array.Empty<string>()
                }
            },
            "card-create");
        var boardGetAfterCreateResponse = await SendJsonRpcAsync(
            client,
            "tools/call",
            new
            {
                name = ToolNames.BoardGet,
                arguments = new { boardId = 1 }
            },
            "board-get-after-create");

        // Assert
        var toolsListBody = await toolsListResponse.Content.ReadAsStringAsync();
        var initialBoardBody = await initialBoardGetResponse.Content.ReadAsStringAsync();
        var createCardBody = await createCardResponse.Content.ReadAsStringAsync();
        var boardGetAfterCreateBody = await boardGetAfterCreateResponse.Content.ReadAsStringAsync();
        Assert.True(
            toolsListResponse.StatusCode == HttpStatusCode.OK,
            $"tools/list expected 200 but got {(int)toolsListResponse.StatusCode}: {toolsListBody}");
        Assert.True(
            initialBoardGetResponse.StatusCode == HttpStatusCode.OK,
            $"board.get expected 200 but got {(int)initialBoardGetResponse.StatusCode}: {initialBoardBody}");
        Assert.True(
            createCardResponse.StatusCode == HttpStatusCode.OK,
            $"card.create expected 200 but got {(int)createCardResponse.StatusCode}: {createCardBody}");
        Assert.True(
            boardGetAfterCreateResponse.StatusCode == HttpStatusCode.OK,
            $"board.get(after) expected 200 but got {(int)boardGetAfterCreateResponse.StatusCode}: {boardGetAfterCreateBody}");

        using var toolsListPayload = await ParseJsonAsync(toolsListResponse);
        using var initialBoardPayload = await ParseJsonAsync(initialBoardGetResponse);
        using var boardAfterCreatePayload = await ParseJsonAsync(boardGetAfterCreateResponse);

        var listedToolNames = toolsListPayload
            .RootElement
            .GetProperty("result")
            .GetProperty("tools")
            .EnumerateArray()
            .Select(x => x.GetProperty("name").GetString())
            .ToArray();
        Assert.Contains(ToolNames.BoardGet, listedToolNames);
        Assert.Contains(ToolNames.CardCreate, listedToolNames);

        var firstColumnId = GetStructuredContent(initialBoardPayload.RootElement)
            .GetProperty("data")
            .GetProperty("columns")[0]
            .GetProperty("columnId")
            .GetInt32();
        Assert.True(firstColumnId > 0);

        var cards = GetStructuredContent(boardAfterCreatePayload.RootElement)
            .GetProperty("data")
            .GetProperty("columns")
            .EnumerateArray()
            .SelectMany(column => column.GetProperty("cards").EnumerateArray())
            .ToArray();

        Assert.Contains(cards, card => card.GetProperty("title").GetString() == "MCP smoke card");
    }

    private static async Task<HttpResponseMessage> SendJsonRpcAsync(HttpClient client, string method, object @params, string id)
    {
        var payload = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = @params
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        return await client.SendAsync(request);
    }

    private static async Task<JsonDocument> ParseJsonAsync(HttpResponseMessage response)
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

        throw new JsonException($"MCP response was neither JSON nor parseable SSE. Raw response: {content}");
    }

    private static string CreateToken(DateTime expiresAtUtc)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TokenSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: TokenIssuer,
            audience: TokenAudience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Name, "mcp-test"),
                new Claim(ClaimTypes.Role, "Admin")
            ],
            notBefore: expiresAtUtc.AddMinutes(-10),
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return handler.WriteToken(token);
    }

    private static JsonElement GetStructuredContent(JsonElement jsonRpcResponse)
    {
        if (!jsonRpcResponse.TryGetProperty("result", out var result))
        {
            throw new KeyNotFoundException($"JSON-RPC response missing result: {jsonRpcResponse}");
        }

        if (!result.TryGetProperty("structuredContent", out var structuredContent))
        {
            throw new KeyNotFoundException($"JSON-RPC result missing structuredContent: {result}");
        }

        return structuredContent;
    }

    private static string BuildDbPath(string dbNamePrefix)
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), ".test-data");
        Directory.CreateDirectory(root);
        return Path.Combine(root, $"{dbNamePrefix}-{Guid.NewGuid():N}.db");
    }
}
