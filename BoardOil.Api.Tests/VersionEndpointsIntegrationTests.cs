using System.Net.Http.Json;
using BoardOil.Api.Tests.Infrastructure;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class VersionEndpointsIntegrationTests
{
    [Fact]
    public async Task VersionEndpoint_ShouldReturnBuildMetadataEnvelope()
    {
        var dbPath = CreateDbPath("boardoil-version-endpoint-tests");

        await using var factory = new BoardOilApiFactory(dbPath);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/version");

        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<BuildMetadataDto>>();
        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
        Assert.Equal(200, envelope.StatusCode);
        Assert.NotNull(envelope.Data);
        Assert.False(string.IsNullOrWhiteSpace(envelope.Data!.Version));
        Assert.False(string.IsNullOrWhiteSpace(envelope.Data.Channel));
        Assert.False(string.IsNullOrWhiteSpace(envelope.Data.Build));
        Assert.False(string.IsNullOrWhiteSpace(envelope.Data.Commit));
    }

    private static string CreateDbPath(string dbNamePrefix)
    {
        var root = Path.Combine(
            Directory.GetCurrentDirectory(),
            ".test-data",
            $"{dbNamePrefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return Path.Combine(root, "boardoil.db");
    }

    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);

    private sealed record BuildMetadataDto(string Version, string Channel, string Build, string Commit);
}
