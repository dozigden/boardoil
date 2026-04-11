using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Auth;
using Microsoft.Data.Sqlite;
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
            "/api/auth/access-tokens",
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
        Assert.Contains("card.get", toolNames);
        Assert.Contains("card.create", toolNames);
    }

    [Fact]
    public async Task PatWithApiReadScope_GetBoard_ShouldReturnOk()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var createdPat = await CreatePatAsync(adminClient, "api-read-token", [MachinePatScopes.ApiRead], "selected", [1]);
        var patClient = CreatePatClient(createdPat.PlainTextToken);

        // Act
        var response = await patClient.GetAsync("/api/boards/1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PatWithoutApiScope_GetBoard_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var createdPat = await CreatePatAsync(adminClient, "mcp-only-token", [MachinePatScopes.McpRead], "selected", [1]);
        var patClient = CreatePatClient(createdPat.PlainTextToken);

        // Act
        var response = await patClient.GetAsync("/api/boards/1");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PatWithApiReadScope_PostBoard_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var createdPat = await CreatePatAsync(adminClient, "api-read-only-token", [MachinePatScopes.ApiRead]);
        var patClient = CreatePatClient(createdPat.PlainTextToken);

        // Act
        var response = await patClient.PostAsJsonAsync("/api/boards", new { name = "Blocked PAT Board" });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PatWithApiWriteScope_PostBoard_WithoutCsrf_ShouldReturnCreated()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var createdPat = await CreatePatAsync(adminClient, "api-write-token", [MachinePatScopes.ApiWrite]);
        var patClient = CreatePatClient(createdPat.PlainTextToken);

        // Act
        var response = await patClient.PostAsJsonAsync("/api/boards", new { name = "PAT Created Board" });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task PatWithSelectedBoardAccess_PostBoard_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var createdPat = await CreatePatAsync(adminClient, "api-write-selected-token", [MachinePatScopes.ApiWrite], "selected", [1]);
        var patClient = CreatePatClient(createdPat.PlainTextToken);

        // Act
        var response = await patClient.PostAsJsonAsync("/api/boards", new { name = "Blocked PAT Board" });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PatWithoutApiSystemScope_GetSystemUsers_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var createdPat = await CreatePatAsync(adminClient, "api-read-token", [MachinePatScopes.ApiRead]);
        var patClient = CreatePatClient(createdPat.PlainTextToken);

        // Act
        var response = await patClient.GetAsync("/api/system/users");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PatWithApiSystemScope_GetSystemUsers_ShouldReturnOk()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var createdPat = await CreatePatAsync(adminClient, "api-system-token", [MachinePatScopes.ApiSystem]);
        var patClient = CreatePatClient(createdPat.PlainTextToken);

        // Act
        var response = await patClient.GetAsync("/api/system/users");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PatWithApiAdminScope_GetSystemBoards_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var createdPat = await CreatePatAsync(adminClient, "api-admin-token", [MachinePatScopes.ApiAdmin]);
        var patClient = CreatePatClient(createdPat.PlainTextToken);

        // Act
        var response = await patClient.GetAsync("/api/system/boards");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PatWithApiSystemScope_GetSystemBoards_ShouldReturnOk()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var createdPat = await CreatePatAsync(adminClient, "api-system-token", [MachinePatScopes.ApiSystem]);
        var patClient = CreatePatClient(createdPat.PlainTextToken);

        // Act
        var response = await patClient.GetAsync("/api/system/boards");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PatWithSelectedBoardAccess_GetBoardOutsideAllowList_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var otherBoardId = await CreateBoardAsAdminAsync(adminClient, "Secondary Board");
        var createdPat = await CreatePatAsync(adminClient, "selected-board-token", [MachinePatScopes.ApiRead], "selected", [1]);
        var patClient = CreatePatClient(createdPat.PlainTextToken);

        // Act
        var response = await patClient.GetAsync($"/api/boards/{otherBoardId}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PatWithSelectedBoardAccess_GetBoards_ShouldFilterToAllowList()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var otherBoardId = await CreateBoardAsAdminAsync(adminClient, "Secondary Board");
        var createdPat = await CreatePatAsync(adminClient, "selected-board-token", [MachinePatScopes.ApiRead], "selected", [1]);
        var patClient = CreatePatClient(createdPat.PlainTextToken);

        // Act
        var response = await patClient.GetAsync("/api/boards");
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<IReadOnlyList<BoardSummaryEnvelope>>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.NotNull(payload!.Data);
        Assert.Contains(payload.Data!, board => board.Id == 1);
        Assert.DoesNotContain(payload.Data!, board => board.Id == otherBoardId);
    }

    [Fact]
    public async Task PatWithReadOnlyScope_CardCreate_ShouldReturnForbiddenToolError()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);

        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/access-tokens",
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
            "/api/auth/access-tokens",
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
            "/api/auth/access-tokens",
            new CreateMachinePatRequest("agent-token", 30, ["mcp:write"], "selected", [1]));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<CreatedMachinePatEnvelope>>();
        Assert.NotNull(created);
        Assert.NotNull(created!.Data);

        // Act
        var revokeResponse = await adminClient.DeleteAsync($"/api/auth/access-tokens/{created.Data!.Token.Id}");
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
    public async Task RevokePat_ShouldBlockFutureRestApiCalls()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var createdPat = await CreatePatAsync(adminClient, "revoked-api-token", [MachinePatScopes.ApiRead], "selected", [1]);
        var patClient = CreatePatClient(createdPat.PlainTextToken);

        // Act
        var revokeResponse = await adminClient.DeleteAsync($"/api/auth/access-tokens/{createdPat.Token.Id}");
        var boardAfterRevokeResponse = await patClient.GetAsync("/api/boards/1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, boardAfterRevokeResponse.StatusCode);
    }

    [Fact]
    public async Task ListPats_ShouldIncludeCreatedTokenMetadata()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/access-tokens",
            new CreateMachinePatRequest("agent-token", 30, ["mcp:write"], "selected", [1]));
        createResponse.EnsureSuccessStatusCode();

        // Act
        var listResponse = await adminClient.GetAsync("/api/auth/access-tokens");
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
    public async Task CreatePat_WithLegacyMcpScope_ShouldReturnBadRequest()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);

        // Act
        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/access-tokens",
            new CreateMachinePatRequest("legacy-scope-token", 30, ["mcp"], "all", []));
        var payload = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<object>>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
    }

    [Fact]
    public async Task PatAuth_WhenLastUsedAtIsAlreadyToday_ShouldNotRewriteTimestamp()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/access-tokens",
            new CreateMachinePatRequest("throttle-same-day-token", 30, ["mcp:read"], "selected", [1]));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<CreatedMachinePatEnvelope>>();
        Assert.NotNull(created);
        Assert.NotNull(created!.Data);

        var todayStartUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        await SetPatLastUsedAtUtcAsync(created.Data!.Token.Id, todayStartUtc);

        // Act
        var toolsListResponse = await SendMcpRequestAsync(
            adminClient,
            created.Data.PlainTextToken,
            "tools/list",
            new { },
            "tools-list-throttle-same-day");
        var tokenAfterCall = await GetMachinePatByIdAsync(adminClient, created.Data.Token.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, toolsListResponse.StatusCode);
        Assert.NotNull(tokenAfterCall);
        Assert.NotNull(tokenAfterCall!.LastUsedAtUtc);
        Assert.Equal(todayStartUtc.Date, tokenAfterCall.LastUsedAtUtc.Value.Date);
        Assert.Equal(TimeSpan.Zero, tokenAfterCall.LastUsedAtUtc.Value.TimeOfDay);
    }

    [Fact]
    public async Task PatAuth_WhenLastUsedAtIsFromPreviousDay_ShouldRewriteTimestamp()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/access-tokens",
            new CreateMachinePatRequest("throttle-stale-token", 30, ["mcp:read"], "selected", [1]));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<CreatedMachinePatEnvelope>>();
        Assert.NotNull(created);
        Assert.NotNull(created!.Data);

        var yesterdayStartUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-1), DateTimeKind.Utc);
        await SetPatLastUsedAtUtcAsync(created.Data!.Token.Id, yesterdayStartUtc);

        // Act
        var toolsListResponse = await SendMcpRequestAsync(
            adminClient,
            created.Data.PlainTextToken,
            "tools/list",
            new { },
            "tools-list-throttle-stale-day");
        var tokenAfterCall = await GetMachinePatByIdAsync(adminClient, created.Data.Token.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, toolsListResponse.StatusCode);
        Assert.NotNull(tokenAfterCall);
        Assert.NotNull(tokenAfterCall!.LastUsedAtUtc);
        Assert.NotEqual(yesterdayStartUtc.Date, tokenAfterCall.LastUsedAtUtc.Value.Date);
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

    [Fact]
    public async Task StandardUser_CanManageOwnAccessTokens()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        _ = await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");

        var standardClient = _factory.CreateClient();
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var createResponse = await standardClient.PostAsJsonAsync(
            "/api/auth/access-tokens",
            new CreateMachinePatRequest("member-token", 30, ["mcp:read"], "selected", [1]));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<CreatedMachinePatEnvelope>>();

        var listResponse = await standardClient.GetAsync("/api/auth/access-tokens");
        var listEnvelope = await listResponse.Content.ReadFromJsonAsync<ApiEnvelope<IReadOnlyList<MachinePatEnvelope>>>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.NotNull(created!.Data);

        var revokeResponse = await standardClient.DeleteAsync($"/api/auth/access-tokens/{created.Data!.Token.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(listEnvelope);
        Assert.NotNull(listEnvelope!.Data);
        var listedToken = Assert.Single(listEnvelope.Data!, token => token.Name == "member-token");
        Assert.Equal(created.Data.Token.Id, listedToken.Id);
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
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

    private static async Task<CreatedMachinePatEnvelope> CreatePatAsync(
        HttpClient client,
        string name,
        IReadOnlyList<string> scopes,
        string boardAccessMode = "all",
        IReadOnlyList<int>? allowedBoardIds = null)
    {
        var createResponse = await client.PostAsJsonAsync(
            "/api/auth/access-tokens",
            new CreateMachinePatRequest(name, 30, scopes, boardAccessMode, allowedBoardIds ?? []));
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<CreatedMachinePatEnvelope>>();
        Assert.NotNull(created);
        Assert.NotNull(created!.Data);
        return created.Data!;
    }

    private HttpClient CreatePatClient(string plainTextToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", plainTextToken);
        return client;
    }

    private static async Task<int> CreateBoardAsAdminAsync(HttpClient adminClient, string boardName)
    {
        var createResponse = await adminClient.PostAsJsonAsync("/api/boards", new { name = boardName });
        createResponse.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        return payload.RootElement.GetProperty("data").GetProperty("id").GetInt32();
    }

    private static string BuildDbPath(string dbNamePrefix)
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), ".test-data");
        Directory.CreateDirectory(root);
        return Path.Combine(root, $"{dbNamePrefix}-{Guid.NewGuid():N}.db");
    }

    private async Task SetPatLastUsedAtUtcAsync(int tokenId, DateTime? lastUsedAtUtc)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE "PersonalAccessTokens"
            SET "LastUsedAtUtc" = $lastUsedAtUtc
            WHERE "Id" = $id;
            """;
        command.Parameters.AddWithValue("$id", tokenId);
        command.Parameters.AddWithValue("$lastUsedAtUtc", lastUsedAtUtc is null ? DBNull.Value : lastUsedAtUtc.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<MachinePatEnvelope?> GetMachinePatByIdAsync(HttpClient client, int tokenId)
    {
        var listResponse = await client.GetAsync("/api/auth/access-tokens");
        var listEnvelope = await listResponse.Content.ReadFromJsonAsync<ApiEnvelope<IReadOnlyList<MachinePatEnvelope>>>();
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(listEnvelope);
        Assert.NotNull(listEnvelope!.Data);
        return listEnvelope.Data!.SingleOrDefault(token => token.Id == tokenId);
    }

    private static async Task<int> CreateUserAsAdminAsync(HttpClient adminClient, string userName, string password, string role)
    {
        var response = await adminClient.PostAsJsonAsync("/api/system/users", new CreateUserRequest(userName, password, role));
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ManagedUserEnvelope>>();
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        return envelope.Data!.Id;
    }

    private static async Task LoginAsAsync(HttpClient client, string userName, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, password));
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AuthSessionEnvelope>>();
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        client.DefaultRequestHeaders.Remove("X-BoardOil-CSRF");
        client.DefaultRequestHeaders.Add("X-BoardOil-CSRF", envelope.Data!.CsrfToken);
    }

    private sealed record LoginRequest(string UserName, string Password);
    private sealed record CreateMachinePatRequest(
        string Name,
        int? ExpiresInDays,
        IReadOnlyList<string> Scopes,
        string BoardAccessMode,
        IReadOnlyList<int> AllowedBoardIds);
    private sealed record CreateUserRequest(string UserName, string Password, string Role);
    private sealed record AuthSessionEnvelope(string CsrfToken);
    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
    private sealed record ManagedUserEnvelope(int Id, string UserName, string Role, bool IsActive, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
    private sealed record BoardSummaryEnvelope(int Id, string Name, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, string? CurrentUserRole);
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
    private static Task<HttpResponseMessage> SendMcpRequestAsync(HttpClient client, string bearerToken, string method, object @params, string id) =>
        McpJsonRpcClient.SendRequestAsync(client, method, @params, id, bearerToken);

    private static Task<JsonDocument> ParseMcpJsonAsync(HttpResponseMessage response) =>
        McpJsonRpcClient.ParseJsonAsync(response);

    private static void AssertMcpForbidden(JsonDocument payload)
    {
        var root = payload.RootElement;
        if (root.TryGetProperty("result", out var result)
            && result.TryGetProperty("structuredContent", out var structuredContent)
            && structuredContent.ValueKind == JsonValueKind.Object
            && structuredContent.TryGetProperty("statusCode", out var statusProperty)
            && statusProperty.TryGetInt32(out var statusCode))
        {
            Assert.Equal(403, statusCode);
            if (structuredContent.TryGetProperty("code", out var codeProperty))
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
