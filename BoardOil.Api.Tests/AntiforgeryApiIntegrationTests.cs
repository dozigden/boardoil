using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class AntiforgeryApiIntegrationTests : ApiFactoryIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public AntiforgeryApiIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("invalid-token")]
    [InlineData("tampered-token")]
    public async Task CookieWrite_WithInvalidHeader_ShouldReturnCsrfFailure(string? header)
    {
        // Arrange
        var client = CreateClient();
        await AuthenticateAsInitialAdminAsync(client);
        if (header == "tampered-token")
        {
            var token = client.DefaultRequestHeaders.GetValues("X-BoardOil-CSRF").Single().ToCharArray();
            var middle = token.Length / 2;
            token[middle] = token[middle] == 'A' ? 'B' : 'A';
            header = new string(token);
        }
        client.DefaultRequestHeaders.Remove("X-BoardOil-CSRF");
        if (header is not null) { client.DefaultRequestHeaders.Add("X-BoardOil-CSRF", header); }

        // Act
        var response = await client.PostAsJsonAsync("/api/boards", new { name = "Rejected" });

        // Assert
        await AssertCsrfFailureAsync(response);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("legacy-equal-token")]
    public async Task CookieWrite_WithMissingOrLegacyCookie_ShouldRejectEvenMatchingHeader(string? cookie)
    {
        // Arrange
        var browser = CreateClient();
        var accessToken = await AuthenticateAsInitialAdminAsync(browser);
        var client = TrackClient(Factory.CreateClient(new() { HandleCookies = false }));
        var cookies = $"boardoil_access={accessToken}";
        if (cookie is not null) { cookies += $"; boardoil_csrf={cookie}"; }
        client.DefaultRequestHeaders.Add("Cookie", cookies);
        client.DefaultRequestHeaders.Add("X-BoardOil-CSRF",
            cookie ?? browser.DefaultRequestHeaders.GetValues("X-BoardOil-CSRF").Single());

        // Act
        var response = await client.PostAsJsonAsync("/api/boards", new { name = "Rejected" });

        // Assert
        await AssertCsrfFailureAsync(response);
    }

    [Fact]
    public async Task CookieWrite_AfterRefreshWithoutAccessCookie_ShouldAcceptExistingRequestToken()
    {
        // Arrange
        await EnsureInitialAdminSeededAsync();
        var browser = CreateClient();
        var login = await browser.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "Password1234!"));
        login.EnsureSuccessStatusCode();
        var refreshCookie = GetCookiePair(login, "boardoil_refresh");
        var tokenResponse = await browser.GetAsync("/api/auth/csrf");
        tokenResponse.EnsureSuccessStatusCode();
        var tokens = await tokenResponse.Content.ReadFromJsonAsync<Envelope<CsrfTokenDto>>();
        Assert.NotNull(tokens?.Data);
        var antiforgeryCookie = GetCookiePair(tokenResponse, "boardoil_csrf");
        var client = TrackClient(Factory.CreateClient(new() { HandleCookies = false }));
        client.DefaultRequestHeaders.Add("Cookie", $"{refreshCookie}; {antiforgeryCookie}");
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { });
        refresh.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", $"{GetCookiePair(refresh, "boardoil_access")}; {antiforgeryCookie}");
        client.DefaultRequestHeaders.Add("X-BoardOil-CSRF", tokens.Data.CsrfToken);

        // Act
        var response = await client.PostAsJsonAsync("/api/boards", new { name = "Refreshed tab" });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.DoesNotContain(refresh.Headers.GetValues("Set-Cookie"), value => value.StartsWith("boardoil_csrf="));
    }

    [Fact]
    public async Task CookieWrite_AfterAnotherTokenIsIssuedForSameUser_ShouldAcceptOriginalToken()
    {
        // Arrange
        var client = CreateClient();
        await AuthenticateAsInitialAdminAsync(client);
        // Simulate another tab acquiring a token with the shared cookie. Keep
        // the original header to prove that a token request does not invalidate it.
        (await client.GetAsync("/api/auth/csrf")).EnsureSuccessStatusCode();

        // Act
        var response = await client.PostAsJsonAsync("/api/boards", new { name = "Original tab" });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CookieWrite_AfterAccountSwitchWithSameAntiforgeryCookie_ShouldRejectOldUserToken()
    {
        // Arrange
        var client = CreateClient();
        await AuthenticateAsInitialAdminAsync(client);
        (await client.PostAsJsonAsync("/api/system/users", new
        {
            userName = "second-user", displayName = "Second user", email = "second@example.test",
            password = "Password1234!", role = "Standard"
        })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("second-user", "Password1234!")))
            .EnsureSuccessStatusCode();
        // Do not change the original tab's token header. Login retains the
        // antiforgery cookie, so rejection must come from identity binding.

        // Act
        var response = await client.PostAsJsonAsync("/api/boards", new { name = "Wrong account" });

        // Assert
        await AssertCsrfFailureAsync(response);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetCsrf_ShouldUseConfiguredNamesAndCookieSecurity(bool allowInsecureCookies)
    {
        // Arrange
        await using var factory = new BoardOilApiFactory(CreateDbPath("antiforgery-options"),
            allowInsecureCookies: allowInsecureCookies,
            configurationOverrides: new Dictionary<string, string?>
            {
                ["BoardOilCsrf:CookieName"] = "test_antiforgery",
                ["BoardOilCsrf:HeaderName"] = "X-Test-Antiforgery"
            });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri(allowInsecureCookies ? "http://localhost" : "https://localhost")
        });
        (await client.PostAsJsonAsync("/api/auth/register-initial-admin",
            new RegisterInitialAdminRequest("admin", "admin@example.test", "Password1234!")))
            .EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/auth/csrf");
        var tokens = await response.Content.ReadFromJsonAsync<Envelope<CsrfTokenDto>>();
        response.EnsureSuccessStatusCode();
        Assert.NotNull(tokens?.Data);
        client.DefaultRequestHeaders.Add("X-Test-Antiforgery", tokens.Data.CsrfToken);

        // Act
        var writeResponse = await client.PostAsJsonAsync("/api/boards", new { name = "Configured token" });

        // Assert
        Assert.Equal(HttpStatusCode.Created, writeResponse.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("test_antiforgery="));
        var attributes = cookie.Split(';', StringSplitOptions.TrimEntries);
        Assert.Contains(attributes, value => value.Equals("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(attributes, value => value.Equals("samesite=strict", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("path=/", attributes);
        Assert.Equal(!allowInsecureCookies, attributes.Any(value => value.Equals("secure", StringComparison.OrdinalIgnoreCase)));
        Assert.NotEqual(attributes[0]["test_antiforgery=".Length..], tokens.Data.CsrfToken);
    }

    [Fact]
    public async Task GetCsrf_WithLegacyCookie_ShouldReplaceItWithUsableFrameworkTokens()
    {
        // Arrange
        var browser = CreateClient();
        var accessToken = await AuthenticateAsInitialAdminAsync(browser);
        var client = TrackClient(Factory.CreateClient(new() { HandleCookies = false }));
        client.DefaultRequestHeaders.Add("Cookie", $"boardoil_access={accessToken}; boardoil_csrf=legacy-token");

        var response = await client.GetAsync("/api/auth/csrf");
        var tokens = await response.Content.ReadFromJsonAsync<Envelope<CsrfTokenDto>>();
        response.EnsureSuccessStatusCode();
        Assert.NotNull(tokens?.Data);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("boardoil_csrf="));
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", $"boardoil_access={accessToken}; {cookie.Split(';')[0]}");
        client.DefaultRequestHeaders.Add("X-BoardOil-CSRF", tokens.Data.CsrfToken);

        // Act
        var writeResponse = await client.PostAsJsonAsync("/api/boards", new { name = "Upgraded token" });

        // Assert
        Assert.Equal(HttpStatusCode.Created, writeResponse.StatusCode);
        Assert.DoesNotContain("legacy-token", cookie);
    }

    [Theory]
    [InlineData("register-initial-admin")]
    [InlineData("login")]
    [InlineData("refresh")]
    public async Task BrowserSessionResponse_ShouldNotIssueCsrfTokens(string operation)
    {
        // Arrange
        var client = CreateClient();
        if (operation != "register-initial-admin") { await AuthenticateAsInitialAdminAsync(client); }
        var body = new { userName = "admin", email = "admin@example.test", password = "Password1234!" };

        // Act
        var response = await client.PostAsJsonAsync($"/api/auth/{operation}", body);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(payload.RootElement.GetProperty("data").TryGetProperty("csrfToken", out _));
        Assert.DoesNotContain(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("boardoil_csrf="));
    }

    [Theory]
    [InlineData("logout")]
    [InlineData("change-password")]
    public async Task SessionEnd_ShouldClearAntiforgeryCookie(string operation)
    {
        // Arrange
        var client = CreateClient();
        await AuthenticateAsInitialAdminAsync(client);

        // Act
        var response = await client.PostAsJsonAsync($"/api/auth/{operation}",
            new { currentPassword = "Password1234!", newPassword = "BetterPassword1234!" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("boardoil_csrf="));
        Assert.StartsWith("boardoil_csrf=;", cookie);
        Assert.Contains("expires=Thu, 01 Jan 1970", cookie);
    }

    private static string GetCookiePair(HttpResponseMessage response, string name) =>
        Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith($"{name}=", StringComparison.Ordinal))
            .Split(';')[0];

    private static async Task AssertCsrfFailureAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(payload.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(403, payload.RootElement.GetProperty("statusCode").GetInt32());
        Assert.Equal("CSRF validation failed.", payload.RootElement.GetProperty("message").GetString());
    }

    private sealed record Envelope<T>(bool Success, T? Data);
}
