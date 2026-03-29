using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class MachinePatIntegrationTests : IAsyncLifetime
{
    private string _databasePath = string.Empty;
    private BoardOilApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _databasePath = BuildDbPath("boardoil-machine-pat-tests");
        _factory = new BoardOilApiFactory(_databasePath);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task CreatePat_ThenCallMcpWithPatBearer_ShouldSucceed()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);

        // Act
        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/machine/pats",
            new CreateMachinePatRequest("agent-token", 30, ["mcp:write"], "selected", [1]));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<CreatedMachinePatEnvelope>>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.NotNull(created!.Data);
        Assert.False(string.IsNullOrWhiteSpace(created.Data!.PlainTextToken));
        Assert.Equal("selected", created.Data.Token.BoardAccessMode);
        Assert.Equal([1], created.Data.Token.AllowedBoardIds);

        var toolsListResponse = await SendMcpRequestAsync(
            adminClient,
            created.Data.PlainTextToken,
            "tools/list",
            new { },
            "tools-list");
        using var toolsListPayload = await ParseMcpJsonAsync(toolsListResponse);

        // Assert
        Assert.Equal(HttpStatusCode.OK, toolsListResponse.StatusCode);
        var toolNames = toolsListPayload.RootElement
            .GetProperty("result")
            .GetProperty("tools")
            .EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString())
            .ToArray();
        Assert.Contains("board.get", toolNames);
        Assert.Contains("card.create", toolNames);
    }

    [Fact]
    public async Task PatWithReadOnlyScope_CardCreate_ShouldReturnForbiddenToolError()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);

        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/machine/pats",
            new CreateMachinePatRequest("read-only-token", 30, ["mcp:read"], "selected", [1]));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<CreatedMachinePatEnvelope>>();
        Assert.NotNull(created);
        Assert.NotNull(created!.Data);

        // Act
        var createCardResponse = await SendMcpRequestAsync(
            adminClient,
            created.Data!.PlainTextToken,
            "tools/call",
            new
            {
                name = "card.create",
                arguments = new
                {
                    boardId = 1,
                    columnId = 1,
                    title = "Blocked by scope",
                    description = "",
                    tagNames = Array.Empty<string>()
                }
            },
            "card-create-forbidden");
        using var payload = await ParseMcpJsonAsync(createCardResponse);

        // Assert
        Assert.Equal(HttpStatusCode.OK, createCardResponse.StatusCode);
        AssertMcpForbidden(payload);
    }

    [Fact]
    public async Task PatWithSelectedBoardAccess_BoardGetOutsideAllowlist_ShouldReturnForbiddenToolError()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/machine/pats",
            new CreateMachinePatRequest("single-board-token", 30, ["mcp:read"], "selected", [1]));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<CreatedMachinePatEnvelope>>();
        Assert.NotNull(created);
        Assert.NotNull(created!.Data);

        // Act
        var boardGetResponse = await SendMcpRequestAsync(
            adminClient,
            created.Data!.PlainTextToken,
            "tools/call",
            new
            {
                name = "board.get",
                arguments = new { id = 2 }
            },
            "board-get-forbidden");
        using var payload = await ParseMcpJsonAsync(boardGetResponse);

        // Assert
        Assert.Equal(HttpStatusCode.OK, boardGetResponse.StatusCode);
        AssertMcpForbidden(payload);
    }

    [Fact]
    public async Task RevokePat_ShouldBlockFuturePatLogin()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/machine/pats",
            new CreateMachinePatRequest("agent-token", 30, ["mcp:write"], "selected", [1]));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<CreatedMachinePatEnvelope>>();
        Assert.NotNull(created);
        Assert.NotNull(created!.Data);

        // Act
        var revokeResponse = await adminClient.DeleteAsync($"/api/auth/machine/pats/{created.Data!.Token.Id}");
        var toolsListAfterRevoke = await SendMcpRequestAsync(
            adminClient,
            created.Data.PlainTextToken,
            "tools/list",
            new { },
            "tools-list-after-revoke");

        // Assert
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, toolsListAfterRevoke.StatusCode);
    }

    [Fact]
    public async Task ListPats_ShouldIncludeCreatedTokenMetadata()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/machine/pats",
            new CreateMachinePatRequest("agent-token", 30, ["mcp:write"], "selected", [1]));
        createResponse.EnsureSuccessStatusCode();

        // Act
        var listResponse = await adminClient.GetAsync("/api/auth/machine/pats");
        var listEnvelope = await listResponse.Content.ReadFromJsonAsync<ApiEnvelope<IReadOnlyList<MachinePatEnvelope>>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(listEnvelope);
        Assert.NotNull(listEnvelope!.Data);
        var token = Assert.Single(listEnvelope.Data!, x => x.Name == "agent-token");
        Assert.Contains("mcp:write", token.Scopes);
        Assert.False(string.IsNullOrWhiteSpace(token.TokenPrefix));
        Assert.Equal("selected", token.BoardAccessMode);
        Assert.Equal([1], token.AllowedBoardIds);
    }

    [Fact]
    public async Task CreatePat_WithLegacyMcpScope_ShouldNormaliseToReadAndWriteScopes()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);

        // Act
        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/machine/pats",
            new CreateMachinePatRequest("legacy-scope-token", 30, ["mcp"], "all", []));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<CreatedMachinePatEnvelope>>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.NotNull(created!.Data);
        Assert.Contains("mcp:read", created.Data!.Token.Scopes);
        Assert.Contains("mcp:write", created.Data.Token.Scopes);
        Assert.Equal("all", created.Data.Token.BoardAccessMode);
        Assert.Empty(created.Data.Token.AllowedBoardIds);
    }

    [Fact]
    public async Task PatLoginEndpoint_ShouldReturnNotFound()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);

        // Act
        var response = await adminClient.PostAsJsonAsync("/api/auth/machine/pat/login", new { token = "bo_pat_test" });

        // Assert
        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Expected 404/405 for removed PAT login endpoint, got {(int)response.StatusCode} ({response.StatusCode}).");
    }

    private static async Task RegisterInitialAdminAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register-initial-admin", new LoginRequest("admin", "Password1234!"));
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AuthSessionEnvelope>>();
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        client.DefaultRequestHeaders.Remove("X-BoardOil-CSRF");
        client.DefaultRequestHeaders.Add("X-BoardOil-CSRF", envelope.Data!.CsrfToken);
    }

    private static string BuildDbPath(string dbNamePrefix)
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), ".test-data");
        Directory.CreateDirectory(root);
        return Path.Combine(root, $"{dbNamePrefix}-{Guid.NewGuid():N}.db");
    }

    private sealed record LoginRequest(string UserName, string Password);
    private sealed record CreateMachinePatRequest(
        string Name,
        int? ExpiresInDays,
        IReadOnlyList<string> Scopes,
        string BoardAccessMode,
        IReadOnlyList<int> AllowedBoardIds);
    private sealed record AuthSessionEnvelope(string CsrfToken);
    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
    private sealed record CreatedMachinePatEnvelope(MachinePatEnvelope Token, string PlainTextToken);
    private sealed record MachinePatEnvelope(
        int Id,
        string Name,
        string TokenPrefix,
        IReadOnlyList<string> Scopes,
        string BoardAccessMode,
        IReadOnlyList<int> AllowedBoardIds,
        DateTime CreatedAtUtc,
        DateTime? ExpiresAtUtc,
        DateTime? LastUsedAtUtc,
        DateTime? RevokedAtUtc);
    private static async Task<HttpResponseMessage> SendMcpRequestAsync(HttpClient client, string bearerToken, string method, object @params, string id)
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
        request.Headers.Authorization = new("Bearer", bearerToken);
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

    private static void AssertMcpForbidden(JsonDocument payload)
    {
        var root = payload.RootElement;
        if (root.TryGetProperty("result", out var result)
            && result.TryGetProperty("structuredContent", out var structuredContent)
            && structuredContent.TryGetProperty("error", out var error)
            && error.ValueKind == JsonValueKind.Object
            && error.TryGetProperty("statusCode", out var statusProperty)
            && statusProperty.TryGetInt32(out var statusCode))
        {
            Assert.Equal(403, statusCode);
            if (error.TryGetProperty("code", out var codeProperty))
            {
                Assert.Equal("forbidden", codeProperty.GetString());
            }

            return;
        }

        if (root.TryGetProperty("error", out var rpcError)
            && rpcError.TryGetProperty("data", out var rpcErrorData)
            && rpcErrorData.TryGetProperty("statusCode", out var rpcStatusProperty)
            && rpcStatusProperty.TryGetInt32(out var rpcStatusCode))
        {
            Assert.Equal(403, rpcStatusCode);
            return;
        }

        Assert.Fail($"Expected MCP forbidden tool error payload, got: {root.GetRawText()}");
    }
}
