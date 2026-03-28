using System.Net;
using System.Net.Http.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Realtime;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class InternalRealtimeEndpointsTests : IAsyncLifetime
{
    private const string RelayApiKey = "test-relay-api-key";

    private BoardOilApiFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        var databasePath = BuildDbPath("boardoil-internal-realtime-tests");
        _factory = new BoardOilApiFactory(databasePath, mcpEventRelayApiKey: RelayApiKey);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task ForwardBoardEvent_WithoutApiKeyHeader_ShouldReturnUnauthorized()
    {
        // Arrange
        var payload = new BoardRealtimeRelayEvent(
            BoardRealtimeEventTypes.CardDeleted,
            1,
            CardId: 1);

        // Act
        var response = await _client.PostAsJsonAsync(BoardRealtimeRelay.EndpointPath, payload);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ForwardBoardEvent_WithValidApiKey_ShouldReturnOk()
    {
        // Arrange
        var payload = new BoardRealtimeRelayEvent(
            BoardRealtimeEventTypes.CardDeleted,
            1,
            CardId: 1);
        using var request = new HttpRequestMessage(HttpMethod.Post, BoardRealtimeRelay.EndpointPath)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add(BoardRealtimeRelay.ApiKeyHeaderName, RelayApiKey);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string BuildDbPath(string dbNamePrefix)
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), ".test-data");
        Directory.CreateDirectory(root);
        return Path.Combine(root, $"{dbNamePrefix}-{Guid.NewGuid():N}.db");
    }
}
