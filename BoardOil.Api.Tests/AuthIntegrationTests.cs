using System.Net;
using System.Net.Http.Json;
using BoardOil.Api.Tests.Infrastructure;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class AuthIntegrationTests : ApiFactoryIntegrationTestBase
{
    [Fact]
    public async Task RegisterInitialAdmin_FirstAttempt_ShouldReturnCreated()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/auth/register-initial-admin",
            new RegisterInitialAdminRequest("admin", "admin@localhost", "Password1234!"));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task RegisterInitialAdmin_WithStaleAccessCookieAndNoCsrfHeader_ShouldSucceed()
    {
        // Arrange
        var client = CreateClient();
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
        var client = CreateClient();

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
    public async Task Login_WithExistingAuthCookieAndNoCsrfHeader_ShouldSucceed()
    {
        // Arrange
        var client = CreateClient();
        _ = await AuthenticateAsInitialAdminAsync(client);
        client.DefaultRequestHeaders.Remove("X-BoardOil-CSRF");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "Password1234!"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ChangeOwnPassword_WithValidCurrentPassword_ShouldReturnOk()
    {
        // Arrange
        var client = CreateClient();
        _ = await AuthenticateAsInitialAdminAsync(client);

        // Act
        var changeResponse = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangeOwnPasswordRequest("Password1234!", "BetterPassword1234!"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = CreateClient();

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
        var dbPath = CreateDbPath("boardoil-auth-secure-cookie-tests");
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

    private sealed record RegisterInitialAdminRequest(string UserName, string Email, string Password);
    private sealed record LoginRequest(string UserName, string Password);
    private sealed record ChangeOwnPasswordRequest(string CurrentPassword, string NewPassword);
    private sealed record BootstrapStatusEnvelope(bool RequiresInitialAdminSetup);
    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
}
