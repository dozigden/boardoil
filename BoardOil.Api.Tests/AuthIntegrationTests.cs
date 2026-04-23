using System.Net;
using System.Net.Http.Json;
using BoardOil.Api.Tests.Infrastructure;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class AuthIntegrationTests : IAsyncLifetime
{
    private string _databasePath = string.Empty;
    private BoardOilApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _databasePath = BuildDbPath("boardoil-auth-tests");
        _factory = new BoardOilApiFactory(_databasePath);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task RegisterInitialAdmin_FirstAttempt_ShouldReturnCreated()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/auth/register-initial-admin",
            new RegisterInitialAdminRequest("admin", "admin@localhost", "Password1234!"));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task RegisterInitialAdmin_WithInvalidEmail_ShouldReturnBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/auth/register-initial-admin",
            new RegisterInitialAdminRequest("admin", "invalid-email", "Password1234!"));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterInitialAdmin_WhenAdminAlreadyExists_ShouldReturnConflict()
    {
        // Arrange
        var client = _factory.CreateClient();
        await RegisterInitialAdminAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/auth/register-initial-admin",
            new RegisterInitialAdminRequest("admin2", "admin2@localhost", "Password1234!"));

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RegisterInitialAdmin_WithStaleAccessCookieAndNoCsrfHeader_ShouldSucceed()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "boardoil_access=stale-token");

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/auth/register-initial-admin",
            new RegisterInitialAdminRequest("admin", "admin@localhost", "Password1234!"));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task BootstrapStatus_WhenNoUsers_ShouldRequireInitialAdminSetup()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/auth/bootstrap-status");
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<BootstrapStatusEnvelope>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        Assert.True(envelope.Data!.RequiresInitialAdminSetup);
    }

    [Fact]
    public async Task BootstrapStatus_AfterInitialAdminRegistration_ShouldNotRequireInitialAdminSetup()
    {
        // Arrange
        var client = _factory.CreateClient();
        await RegisterInitialAdminAsync(client);

        // Act
        var response = await client.GetAsync("/api/auth/bootstrap-status");
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<BootstrapStatusEnvelope>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        Assert.False(envelope.Data!.RequiresInitialAdminSetup);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        await RegisterInitialAdminAsync(client);

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "wrong-password"));
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal(401, payload.StatusCode);
    }

    [Fact]
    public async Task Login_WithExistingAuthCookieAndNoCsrfHeader_ShouldSucceed()
    {
        // Arrange
        var client = _factory.CreateClient();
        await RegisterInitialAdminAsync(client);
        client.DefaultRequestHeaders.Remove("X-BoardOil-CSRF");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "Password1234!"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ChangeOwnPassword_WithValidCurrentPassword_ShouldRequireNewPasswordForSubsequentLogin()
    {
        // Arrange
        var client = _factory.CreateClient();
        await RegisterInitialAdminAsync(client);

        // Act
        var changeResponse = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangeOwnPasswordRequest("Password1234!", "BetterPassword1234!"));
        var oldLoginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "Password1234!"));
        var newLoginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "BetterPassword1234!"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLoginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newLoginResponse.StatusCode);
    }

    [Fact]
    public async Task ChangeOwnPassword_WithInvalidCurrentPassword_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        await RegisterInitialAdminAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangeOwnPasswordRequest("wrong-password", "BetterPassword1234!"));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangeOwnPassword_ShouldRevokeExistingRefreshToken()
    {
        // Arrange
        var client = _factory.CreateClient();
        var refreshToken = await RegisterInitialAdminAndGetRefreshTokenAsync(client);

        // Act
        var changeResponse = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangeOwnPasswordRequest("Password1234!", "BetterPassword1234!"));
        var refreshProbeClient = _factory.CreateClient();
        refreshProbeClient.DefaultRequestHeaders.Add("Cookie", $"boardoil_refresh={refreshToken}");
        var refreshResponse = await refreshProbeClient.PostAsJsonAsync("/api/auth/refresh", new { });

        // Assert
        Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task ChangeOwnPassword_WithInvalidNewPassword_ShouldReturnBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        await RegisterInitialAdminAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangeOwnPasswordRequest("Password1234!", "short"));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/refresh", new { });
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal(401, payload.StatusCode);
    }

    [Fact]
    public async Task RegisterInitialAdmin_WhenInsecureCookiesDisabled_ShouldSetSecureFlagOnAuthCookies()
    {
        // Arrange
        var dbPath = BuildDbPath("boardoil-auth-secure-cookie-tests");
        await using var factory = new BoardOilApiFactory(dbPath, allowInsecureCookies: false);
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/auth/register-initial-admin",
            new RegisterInitialAdminRequest("admin", "admin@localhost", "Password1234!"));

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.True(ResponseHasCookieAttribute(response, "boardoil_access", "secure"));
        Assert.True(ResponseHasCookieAttribute(response, "boardoil_refresh", "secure"));
    }

    [Fact]
    public async Task RegisterInitialAdmin_WhenInsecureCookiesEnabled_ShouldNotSetSecureFlagOnAuthCookies()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/auth/register-initial-admin",
            new RegisterInitialAdminRequest("admin", "admin@localhost", "Password1234!"));

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.False(ResponseHasCookieAttribute(response, "boardoil_access", "secure"));
        Assert.False(ResponseHasCookieAttribute(response, "boardoil_refresh", "secure"));
    }

    private static async Task RegisterInitialAdminAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register-initial-admin",
            new RegisterInitialAdminRequest("admin", "admin@localhost", "Password1234!"));
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AuthSessionEnvelope>>();
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        client.DefaultRequestHeaders.Remove("X-BoardOil-CSRF");
        client.DefaultRequestHeaders.Add("X-BoardOil-CSRF", envelope.Data!.CsrfToken);
    }

    private static async Task<string> RegisterInitialAdminAndGetRefreshTokenAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register-initial-admin",
            new RegisterInitialAdminRequest("admin", "admin@localhost", "Password1234!"));
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AuthSessionEnvelope>>();
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        client.DefaultRequestHeaders.Remove("X-BoardOil-CSRF");
        client.DefaultRequestHeaders.Add("X-BoardOil-CSRF", envelope.Data!.CsrfToken);

        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookieHeaders));
        var refreshCookie = cookieHeaders.FirstOrDefault(x => x.StartsWith("boardoil_refresh=", StringComparison.OrdinalIgnoreCase));
        Assert.False(string.IsNullOrWhiteSpace(refreshCookie));
        var cookieValue = refreshCookie!.Split(';', StringSplitOptions.RemoveEmptyEntries)[0];
        return cookieValue["boardoil_refresh=".Length..];
    }

    private static bool ResponseHasCookieAttribute(HttpResponseMessage response, string cookieName, string attribute)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return false;
        }

        var prefix = $"{cookieName}=";
        foreach (var value in values)
        {
            if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var segments = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Any(x => x.Equals(attribute, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildDbPath(string dbNamePrefix)
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), ".test-data");
        Directory.CreateDirectory(root);
        return Path.Combine(root, $"{dbNamePrefix}-{Guid.NewGuid():N}.db");
    }

    private sealed record RegisterInitialAdminRequest(string UserName, string Email, string Password);
    private sealed record LoginRequest(string UserName, string Password);
    private sealed record ChangeOwnPasswordRequest(string CurrentPassword, string NewPassword);
    private sealed record AuthSessionEnvelope(string CsrfToken);
    private sealed record BootstrapStatusEnvelope(bool RequiresInitialAdminSetup);
    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
}
