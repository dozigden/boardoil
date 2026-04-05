using System.Net.Http.Json;
using BoardOil.Api.Tests.Infrastructure;
using Xunit;

namespace BoardOil.Api.Tests;

public abstract class McpIntegrationTestBase : IAsyncLifetime
{
    private string _databasePath = string.Empty;
    private BoardOilApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _databasePath = BuildDbPath(GetType().Name);
        _factory = new BoardOilApiFactory(_databasePath);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    protected HttpClient CreateClient() => _factory.CreateClient();

    protected static async Task RegisterInitialAdminAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register-initial-admin", new LoginRequest("admin", "Password1234!"));
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AuthSessionEnvelope>>();
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        client.DefaultRequestHeaders.Remove("X-BoardOil-CSRF");
        client.DefaultRequestHeaders.Add("X-BoardOil-CSRF", envelope.Data!.CsrfToken);
    }

    protected static async Task<string> LoginMachineAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/machine/login", new LoginRequest("admin", "Password1234!"));
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<MachineSessionEnvelope>>();
        Assert.NotNull(payload);
        Assert.NotNull(payload!.Data);
        return payload.Data!.AccessToken;
    }

    protected static async Task<string> CreateMachinePatAsync(
        HttpClient client,
        IReadOnlyList<string>? scopes = null,
        string boardAccessMode = "selected",
        IReadOnlyList<int>? allowedBoardIds = null)
    {
        var request = new CreateMachinePatRequest(
            "api-mcp-test-token",
            30,
            scopes ?? ["mcp:read", "mcp:write"],
            boardAccessMode,
            allowedBoardIds ?? [1]);

        var response = await client.PostAsJsonAsync("/api/auth/access-tokens", request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<CreatedMachinePatEnvelope>>();
        Assert.NotNull(payload);
        Assert.NotNull(payload!.Data);
        Assert.False(string.IsNullOrWhiteSpace(payload.Data!.PlainTextToken));
        return payload.Data.PlainTextToken;
    }

    private static string BuildDbPath(string dbNamePrefix)
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), ".test-data");
        Directory.CreateDirectory(root);
        return Path.Combine(root, $"{dbNamePrefix}-{Guid.NewGuid():N}.db");
    }

    protected sealed record LoginRequest(string UserName, string Password);
    protected sealed record CreateMachinePatRequest(
        string Name,
        int? ExpiresInDays,
        IReadOnlyList<string> Scopes,
        string BoardAccessMode,
        IReadOnlyList<int> AllowedBoardIds);
    protected sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
    protected sealed record AuthSessionEnvelope(string CsrfToken);
    protected sealed record CreatedMachinePatEnvelope(MachinePatEnvelope Token, string PlainTextToken);
    protected sealed record MachineSessionEnvelope(string AccessToken);
    protected sealed record MachinePatEnvelope(
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
}
