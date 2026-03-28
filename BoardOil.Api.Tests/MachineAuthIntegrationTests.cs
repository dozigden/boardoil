using System.Net;
using System.Net.Http.Json;
using BoardOil.Api.Tests.Infrastructure;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class MachineAuthIntegrationTests : IAsyncLifetime
{
    private string _databasePath = string.Empty;
    private BoardOilApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _databasePath = BuildDbPath("boardoil-machine-auth-tests");
        _factory = new BoardOilApiFactory(_databasePath);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task MachineLogin_WithValidCredentials_ShouldReturnAccessAndRefreshTokens()
    {
        // Arrange
        var client = _factory.CreateClient();
        await RegisterInitialAdminAsync(client);

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/machine/login", new LoginRequest("admin", "Password1234!"));
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<MachineAuthEnvelope>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.False(string.IsNullOrWhiteSpace(envelope.Data!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(envelope.Data.RefreshToken));
        Assert.Equal("Bearer", envelope.Data.TokenType);
    }

    [Fact]
    public async Task MachineRefresh_WithValidToken_ShouldReturnNewTokenPair()
    {
        // Arrange
        var client = _factory.CreateClient();
        await RegisterInitialAdminAsync(client);
        var loginResponse = await client.PostAsJsonAsync("/api/auth/machine/login", new LoginRequest("admin", "Password1234!"));
        loginResponse.EnsureSuccessStatusCode();
        var loginEnvelope = await loginResponse.Content.ReadFromJsonAsync<ApiEnvelope<MachineAuthEnvelope>>();
        Assert.NotNull(loginEnvelope);
        Assert.NotNull(loginEnvelope!.Data);

        // Act
        var refreshResponse = await client.PostAsJsonAsync("/api/auth/machine/refresh", new MachineRefreshRequest(loginEnvelope.Data!.RefreshToken));
        var refreshEnvelope = await refreshResponse.Content.ReadFromJsonAsync<ApiEnvelope<MachineAuthEnvelope>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        Assert.NotNull(refreshEnvelope);
        Assert.NotNull(refreshEnvelope!.Data);
        Assert.NotEqual(loginEnvelope.Data!.RefreshToken, refreshEnvelope.Data!.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(refreshEnvelope.Data.AccessToken));
    }

    [Fact]
    public async Task MachineRefresh_WithInvalidToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/machine/refresh", new MachineRefreshRequest("invalid-refresh-token"));
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.False(envelope!.Success);
    }

    [Fact]
    public async Task MachineLogout_ShouldRevokeRefreshToken()
    {
        // Arrange
        var client = _factory.CreateClient();
        await RegisterInitialAdminAsync(client);
        var loginResponse = await client.PostAsJsonAsync("/api/auth/machine/login", new LoginRequest("admin", "Password1234!"));
        loginResponse.EnsureSuccessStatusCode();
        var loginEnvelope = await loginResponse.Content.ReadFromJsonAsync<ApiEnvelope<MachineAuthEnvelope>>();
        Assert.NotNull(loginEnvelope);
        Assert.NotNull(loginEnvelope!.Data);

        // Act
        var logoutResponse = await client.PostAsJsonAsync("/api/auth/machine/logout", new MachineLogoutRequest(loginEnvelope.Data!.RefreshToken));
        var refreshAfterLogoutResponse = await client.PostAsJsonAsync("/api/auth/machine/refresh", new MachineRefreshRequest(loginEnvelope.Data.RefreshToken));

        // Assert
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogoutResponse.StatusCode);
    }

    private static async Task RegisterInitialAdminAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register-initial-admin", new LoginRequest("admin", "Password1234!"));
        response.EnsureSuccessStatusCode();
    }

    private static string BuildDbPath(string dbNamePrefix)
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), ".test-data");
        Directory.CreateDirectory(root);
        return Path.Combine(root, $"{dbNamePrefix}-{Guid.NewGuid():N}.db");
    }

    private sealed record LoginRequest(string UserName, string Password);
    private sealed record MachineRefreshRequest(string RefreshToken);
    private sealed record MachineLogoutRequest(string? RefreshToken);
    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
    private sealed record MachineAuthEnvelope(
        string AccessToken,
        DateTime AccessTokenExpiresAtUtc,
        string RefreshToken,
        DateTime RefreshTokenExpiresAtUtc,
        UserEnvelope User,
        string TokenType);
    private sealed record UserEnvelope(int Id, string UserName, string Role);
}
