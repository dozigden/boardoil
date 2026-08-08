using System.Net;
using BoardOil.Api.Tests.Infrastructure;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class JwtSigningKeyStartupIntegrationTests
{
    [Fact]
    public async Task Startup_WithoutConfiguredSigningKey_ShouldPersistAndReuseGeneratedKey()
    {
        // Arrange
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "boardoil-signing-key-startup-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        var databasePath = Path.Combine(directoryPath, "boardoil.db");
        var signingKeyPath = Path.Combine(directoryPath, "boardoil-auth-signing-key");
        var configurationOverrides = new Dictionary<string, string?>
        {
            ["BoardOilAuth:SigningKey"] = null
        };

        try
        {
            // Act
            await using (var firstFactory = new BoardOilApiFactory(
                databasePath,
                configurationOverrides: configurationOverrides))
            {
                using var firstClient = firstFactory.CreateClient();
                var firstResponse = await firstClient.GetAsync("/api/health");
                Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
            }

            var firstKey = await File.ReadAllTextAsync(signingKeyPath);

            await using (var secondFactory = new BoardOilApiFactory(
                databasePath,
                configurationOverrides: configurationOverrides))
            {
                using var secondClient = secondFactory.CreateClient();
                var secondResponse = await secondClient.GetAsync("/api/health");
                Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
            }

            // Assert
            Assert.True(firstKey.Length >= 32);
            Assert.Equal(firstKey, await File.ReadAllTextAsync(signingKeyPath));
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }
}
