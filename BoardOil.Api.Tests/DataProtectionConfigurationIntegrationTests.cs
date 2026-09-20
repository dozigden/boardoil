using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class DataProtectionConfigurationIntegrationTests
{
    [Fact]
    public async Task DataProtectionProvider_WithinApplicationLifetime_ShouldRoundTripPayload()
    {
        // Arrange
        var databasePath = CreateDatabasePath();

        try
        {
            await using var factory = new BoardOilApiFactory(databasePath);
            var provider = factory.Services.GetRequiredService<IDataProtectionProvider>();
            var protector = provider.CreateProtector(nameof(DataProtectionConfigurationIntegrationTests));

            // Act
            var protectedPayload = protector.Protect("oauth-consent");
            var payload = protector.Unprotect(protectedPayload);

            // Assert
            Assert.IsType<EphemeralDataProtectionProvider>(provider);
            Assert.Equal("oauth-consent", payload);
        }
        finally
        {
            DeleteTestDirectory(databasePath);
        }
    }

    [Fact]
    public async Task ApplicationStartup_ShouldNotAccessDefaultDataProtectionKeyManager()
    {
        // Arrange
        var databasePath = CreateDatabasePath();
        var keyManager = new TrackingKeyManager();

        try
        {
            await using var factory = new BoardOilApiFactory(
                databasePath,
                configureTestServices: services =>
                    services.Replace(ServiceDescriptor.Singleton<IKeyManager>(keyManager)));

            // Act
            _ = factory.Services;

            // Assert
            Assert.False(keyManager.WasAccessed);
        }
        finally
        {
            DeleteTestDirectory(databasePath);
        }
    }

    [Fact]
    public async Task DataProtectionProvider_AfterApplicationRestart_ShouldRejectPreviousPayload()
    {
        // Arrange
        var databasePath = CreateDatabasePath();
        string protectedPayload;

        try
        {
            await using (var firstFactory = new BoardOilApiFactory(databasePath))
            {
                var firstProvider = firstFactory.Services.GetRequiredService<IDataProtectionProvider>();
                var firstProtector = firstProvider.CreateProtector(nameof(DataProtectionConfigurationIntegrationTests));
                protectedPayload = firstProtector.Protect("oauth-consent");
            }

            await using var restartedFactory = new BoardOilApiFactory(databasePath);
            var restartedProvider = restartedFactory.Services.GetRequiredService<IDataProtectionProvider>();
            var restartedProtector = restartedProvider.CreateProtector(nameof(DataProtectionConfigurationIntegrationTests));

            // Act
            var exception = Record.Exception(() => restartedProtector.Unprotect(protectedPayload));

            // Assert
            Assert.IsType<CryptographicException>(exception);
        }
        finally
        {
            DeleteTestDirectory(databasePath);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CookieWrite_AfterApplicationRestart_ShouldRequireFreshAntiforgeryTokens(bool acquireFreshTokens)
    {
        // Arrange
        var databasePath = CreateDatabasePath();
        try
        {
            string accessCookie;
            string antiforgeryCookie;
            string requestToken;
            await using (var firstFactory = new BoardOilApiFactory(databasePath))
            {
                using var firstClient = firstFactory.CreateClient();
                var registration = await firstClient.PostAsJsonAsync("/api/auth/register-initial-admin",
                    new RegisterInitialAdminRequest("admin", "admin@example.test", "Password1234!"));
                registration.EnsureSuccessStatusCode();
                accessCookie = GetCookiePair(registration, "boardoil_access");
                var tokens = await firstClient.GetAsync("/api/auth/csrf");
                tokens.EnsureSuccessStatusCode();
                antiforgeryCookie = GetCookiePair(tokens, "boardoil_csrf");
                var payload = await tokens.Content.ReadFromJsonAsync<ApiResult<CsrfTokenDto>>();
                Assert.NotNull(payload?.Data);
                requestToken = payload.Data.CsrfToken;
            }

            // Preserve database, signing key and browser cookies across a new application lifetime.
            await using var restartedFactory = new BoardOilApiFactory(databasePath);
            using var client = restartedFactory.CreateClient(new() { HandleCookies = false });
            client.DefaultRequestHeaders.Add("Cookie", $"{accessCookie}; {antiforgeryCookie}");
            (await client.GetAsync("/api/auth/me")).EnsureSuccessStatusCode();
            if (acquireFreshTokens)
            {
                var tokens = await client.GetAsync("/api/auth/csrf");
                tokens.EnsureSuccessStatusCode();
                var payload = await tokens.Content.ReadFromJsonAsync<ApiResult<CsrfTokenDto>>();
                Assert.NotNull(payload?.Data);
                requestToken = payload.Data.CsrfToken;
                antiforgeryCookie = GetCookiePair(tokens, "boardoil_csrf");
                client.DefaultRequestHeaders.Remove("Cookie");
                client.DefaultRequestHeaders.Add("Cookie", $"{accessCookie}; {antiforgeryCookie}");
            }
            client.DefaultRequestHeaders.Add("X-BoardOil-CSRF", requestToken);

            // Act
            var response = await client.PostAsJsonAsync("/api/boards", new { name = "After restart" });

            // Assert
            if (acquireFreshTokens)
            {
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            }
            else
            {
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                var payload = await response.Content.ReadFromJsonAsync<ApiResult>();
                Assert.NotNull(payload);
                Assert.False(payload.Success);
                Assert.Equal(403, payload.StatusCode);
                Assert.Equal("CSRF validation failed.", payload.Message);
            }
        }
        finally
        {
            DeleteTestDirectory(databasePath);
        }
    }

    private static string GetCookiePair(HttpResponseMessage response, string name) =>
        Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith($"{name}=", StringComparison.Ordinal))
            .Split(';')[0];

    private static string CreateDatabasePath() =>
        Path.Combine(
            Path.GetTempPath(),
            "boardoil-data-protection-tests",
            Guid.NewGuid().ToString("N"),
            "boardoil.db");

    private static void DeleteTestDirectory(string databasePath)
    {
        var directoryPath = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    private sealed class TrackingKeyManager : IKeyManager
    {
        private int _wasAccessed;

        public bool WasAccessed => Volatile.Read(ref _wasAccessed) != 0;

        public IKey CreateNewKey(DateTimeOffset activationDate, DateTimeOffset expirationDate)
        {
            RecordAccess();
            throw new NotSupportedException();
        }

        public IReadOnlyCollection<IKey> GetAllKeys()
        {
            RecordAccess();
            return Array.Empty<IKey>();
        }

        public CancellationToken GetCacheExpirationToken()
        {
            RecordAccess();
            return CancellationToken.None;
        }

        public void RevokeAllKeys(DateTimeOffset revocationDate, string? reason = null) =>
            RecordAccess();

        public void RevokeKey(Guid keyId, string? reason = null) =>
            RecordAccess();

        private void RecordAccess() => Interlocked.Exchange(ref _wasAccessed, 1);
    }
}
