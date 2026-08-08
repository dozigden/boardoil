using BoardOil.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class JwtSigningKeyProviderTests
{
    [Fact]
    public void Resolve_WithoutConfiguredKey_ShouldGenerateAndReusePersistedKey()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var keyPath = Path.Combine(directory.Path, "nested", "boardoil-auth-signing-key");
        var configuration = CreateConfiguration();

        // Act
        var firstKey = JwtSigningKeyProvider.Resolve(configuration, keyPath);
        var secondKey = JwtSigningKeyProvider.Resolve(configuration, keyPath);

        // Assert
        Assert.Equal(firstKey, secondKey);
        Assert.Equal(firstKey, File.ReadAllText(keyPath));
        Assert.True(firstKey.Length >= 32);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(keyPath)!, "*.tmp"));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(keyPath));
        }
    }

    [Fact]
    public void Resolve_WithUniqueExplicitKey_ShouldUseConfiguredKeyWithoutCreatingFile()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var keyPath = Path.Combine(directory.Path, "boardoil-auth-signing-key");
        const string configuredKey = "unique-explicit-signing-key-12345678901234567890";
        var configuration = CreateConfiguration(configuredKey);

        // Act
        var resolvedKey = JwtSigningKeyProvider.Resolve(configuration, keyPath);

        // Assert
        Assert.Equal(configuredKey, resolvedKey);
        Assert.False(File.Exists(keyPath));
    }

    [Theory]
    [InlineData(JwtSigningKeyProvider.FormerPublishedSigningKey)]
    [InlineData(JwtSigningKeyProvider.FormerPublishedDevelopmentSigningKey)]
    public void Resolve_WithFormerPublishedKey_ShouldGenerateReplacement(string publishedKey)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var keyPath = Path.Combine(directory.Path, "boardoil-auth-signing-key");
        var configuration = CreateConfiguration(publishedKey);

        // Act
        var resolvedKey = JwtSigningKeyProvider.Resolve(configuration, keyPath);

        // Assert
        Assert.NotEqual(publishedKey, resolvedKey);
        Assert.Equal(resolvedKey, File.ReadAllText(keyPath));
    }

    [Fact]
    public async Task Resolve_WithConcurrentFirstStarts_ShouldReturnOnePersistedKey()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var keyPath = Path.Combine(directory.Path, "nested", "boardoil-auth-signing-key");
        var configuration = CreateConfiguration();
        using var startBarrier = new Barrier(participantCount: 8);

        // Act
        var resolutions = await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(_ => Task.Factory.StartNew(
                    () =>
                    {
                        startBarrier.SignalAndWait();
                        return JwtSigningKeyProvider.Resolve(configuration, keyPath);
                    },
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default)));

        // Assert
        Assert.Single(resolutions.Distinct(StringComparer.Ordinal));
        Assert.Equal(resolutions[0], File.ReadAllText(keyPath));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(keyPath)!, "*.tmp"));
    }

    [Fact]
    public void Resolve_WithInvalidPersistedKey_ShouldExplainRecovery()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var keyPath = Path.Combine(directory.Path, "boardoil-auth-signing-key");
        File.WriteAllText(keyPath, "too-short");
        var configuration = CreateConfiguration();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => JwtSigningKeyProvider.Resolve(configuration, keyPath));

        // Assert
        Assert.Contains(keyPath, exception.Message);
        Assert.Contains("Delete the file to regenerate", exception.Message);
        Assert.Contains("BoardOilAuth:SigningKey", exception.Message);
    }

    private static IConfiguration CreateConfiguration(string? signingKey = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BoardOilAuth:SigningKey"] = signingKey
            })
            .Build();

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "boardoil-signing-key-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
