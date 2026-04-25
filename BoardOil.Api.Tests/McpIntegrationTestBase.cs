using System.Net.Http.Json;
using BoardOil.Api.Tests.Infrastructure;
using Xunit;

namespace BoardOil.Api.Tests;

public abstract class McpIntegrationTestBase : ApiFactoryIntegrationTestBase
{
    protected async Task RegisterInitialAdminAsync(HttpClient client)
    {
        _ = await AuthenticateAsInitialAdminAsync(client);
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
        IReadOnlyList<string>? scopes = null)
    {
        var request = new CreateMachinePatRequest(
            "api-mcp-test-token",
            30,
            scopes ?? ["mcp:read", "mcp:write"]);

        var response = await client.PostAsJsonAsync("/api/auth/access-tokens", request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<CreatedMachinePatEnvelope>>();
        Assert.NotNull(payload);
        Assert.NotNull(payload!.Data);
        Assert.False(string.IsNullOrWhiteSpace(payload.Data!.PlainTextToken));
        return payload.Data.PlainTextToken;
    }

    protected sealed record LoginRequest(string UserName, string Password);
    protected sealed record CreateMachinePatRequest(
        string Name,
        int? ExpiresInDays,
        IReadOnlyList<string> Scopes);
    protected sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
    protected sealed record CreatedMachinePatEnvelope(MachinePatEnvelope Token, string PlainTextToken);
    protected sealed record MachineSessionEnvelope(string AccessToken);
    protected sealed record MachinePatEnvelope(
        int Id,
        string Name,
        string TokenPrefix,
        IReadOnlyList<string> Scopes,
        DateTime CreatedAtUtc,
        DateTime? ExpiresAtUtc,
        DateTime? LastUsedAtUtc,
        DateTime? RevokedAtUtc);
}
