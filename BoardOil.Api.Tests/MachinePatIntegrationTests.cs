using System.Net;
using System.Net.Http.Json;
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
    public async Task CreatePat_ThenLoginWithPat_ShouldReturnMachineSession()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);

        // Act
        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/machine/pats",
            new CreateMachinePatRequest("agent-token", 30, ["mcp"]));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<CreatedMachinePatEnvelope>>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.NotNull(created!.Data);
        Assert.False(string.IsNullOrWhiteSpace(created.Data!.PlainTextToken));

        var loginResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/machine/pat/login",
            new MachinePatLoginRequest(created.Data.PlainTextToken));
        var loginEnvelope = await loginResponse.Content.ReadFromJsonAsync<ApiEnvelope<MachineAuthEnvelope>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(loginEnvelope);
        Assert.NotNull(loginEnvelope!.Data);
        Assert.False(string.IsNullOrWhiteSpace(loginEnvelope.Data!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(loginEnvelope.Data.RefreshToken));
        Assert.Equal("Bearer", loginEnvelope.Data.TokenType);
    }

    [Fact]
    public async Task RevokePat_ShouldBlockFuturePatLogin()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/machine/pats",
            new CreateMachinePatRequest("agent-token", 30, ["mcp"]));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<CreatedMachinePatEnvelope>>();
        Assert.NotNull(created);
        Assert.NotNull(created!.Data);

        // Act
        var revokeResponse = await adminClient.DeleteAsync($"/api/auth/machine/pats/{created.Data!.Token.Id}");
        var loginAfterRevoke = await adminClient.PostAsJsonAsync(
            "/api/auth/machine/pat/login",
            new MachinePatLoginRequest(created.Data.PlainTextToken));

        // Assert
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, loginAfterRevoke.StatusCode);
    }

    [Fact]
    public async Task ListPats_ShouldIncludeCreatedTokenMetadata()
    {
        // Arrange
        var adminClient = _factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/machine/pats",
            new CreateMachinePatRequest("agent-token", 30, ["mcp"]));
        createResponse.EnsureSuccessStatusCode();

        // Act
        var listResponse = await adminClient.GetAsync("/api/auth/machine/pats");
        var listEnvelope = await listResponse.Content.ReadFromJsonAsync<ApiEnvelope<IReadOnlyList<MachinePatEnvelope>>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(listEnvelope);
        Assert.NotNull(listEnvelope!.Data);
        var token = Assert.Single(listEnvelope.Data!, x => x.Name == "agent-token");
        Assert.Contains("mcp", token.Scopes);
        Assert.False(string.IsNullOrWhiteSpace(token.TokenPrefix));
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
    private sealed record CreateMachinePatRequest(string Name, int? ExpiresInDays, IReadOnlyList<string> Scopes);
    private sealed record MachinePatLoginRequest(string Token);
    private sealed record AuthSessionEnvelope(string CsrfToken);
    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
    private sealed record CreatedMachinePatEnvelope(MachinePatEnvelope Token, string PlainTextToken);
    private sealed record MachinePatEnvelope(
        int Id,
        string Name,
        string TokenPrefix,
        IReadOnlyList<string> Scopes,
        DateTime CreatedAtUtc,
        DateTime? ExpiresAtUtc,
        DateTime? LastUsedAtUtc,
        DateTime? RevokedAtUtc);
    private sealed record MachineAuthEnvelope(
        string AccessToken,
        DateTime AccessTokenExpiresAtUtc,
        string RefreshToken,
        DateTime RefreshTokenExpiresAtUtc,
        UserEnvelope User,
        string TokenType);
    private sealed record UserEnvelope(int Id, string UserName, string Role);
}
