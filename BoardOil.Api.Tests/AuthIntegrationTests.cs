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
        var response = await client.PostAsJsonAsync("/api/auth/register-initial-admin", new LoginRequest("admin", "Password1234!"));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task RegisterInitialAdmin_WhenAdminAlreadyExists_ShouldReturnConflict()
    {
        // Arrange
        var client = _factory.CreateClient();
        await RegisterInitialAdminAsync(client);

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register-initial-admin", new LoginRequest("admin2", "Password1234!"));

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
        var response = await client.PostAsJsonAsync("/api/auth/register-initial-admin", new LoginRequest("admin", "Password1234!"));

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
        var response = await client.PostAsJsonAsync("/api/auth/register-initial-admin", new LoginRequest("admin", "Password1234!"));

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
        var response = await client.PostAsJsonAsync("/api/auth/register-initial-admin", new LoginRequest("admin", "Password1234!"));

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.False(ResponseHasCookieAttribute(response, "boardoil_access", "secure"));
        Assert.False(ResponseHasCookieAttribute(response, "boardoil_refresh", "secure"));
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

    private sealed record LoginRequest(string UserName, string Password);
    private sealed record AuthSessionEnvelope(string CsrfToken);
    private sealed record BootstrapStatusEnvelope(bool RequiresInitialAdminSetup);
    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
}
