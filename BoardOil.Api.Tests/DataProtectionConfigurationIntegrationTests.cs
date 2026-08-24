using System.Security.Cryptography;
using BoardOil.Api.Tests.Infrastructure;
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
