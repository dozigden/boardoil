using System.Net.Http.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Column;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class RealtimeIntegrationTests : TestBaseIntegration
{
    protected override string DbNamePrefix => "boardoil-realtime-tests";

    [Fact]
    public async Task HubConnection_AnonymousClient_ShouldBeRejected()
    {
        await using var anonymousConnection = CreateHubConnection(authenticated: false);

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => anonymousConnection.StartAsync());
        var statusCode = (ex as HttpRequestException)?.StatusCode;
        var messageHasAuthCode = ex.Message.Contains("401", StringComparison.Ordinal)
            || ex.Message.Contains("403", StringComparison.Ordinal);

        Assert.True(
            statusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden || messageHasAuthCode,
            $"Expected unauthorized/forbidden negotiate failure but got: {ex.GetType().Name} - {ex.Message}");
    }

    [Fact]
    public async Task CardCreated_ShouldBroadcastToTwoConnectedClients()
    {
        // Arrange
        var column = await CreateColumnAsync("Todo");

        await using var connectionA = CreateHubConnection();
        await using var connectionB = CreateHubConnection();

        var eventA = new TaskCompletionSource<CardDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var eventB = new TaskCompletionSource<CardDto>(TaskCreationOptions.RunContinuationsAsynchronously);

        connectionA.On<CardDto>("CardCreated", card => eventA.TrySetResult(card));
        connectionB.On<CardDto>("CardCreated", card => eventB.TrySetResult(card));

        await StartConnectionsAsync(connectionA, connectionB);

        // Act
        var createCardResponse = await Client.PostAsJsonAsync(
            "/api/cards",
            new CreateCardRequest(column.Id, "Realtime Task", "Desc", null));
        createCardResponse.EnsureSuccessStatusCode();

        // Assert
        var cardA = await WaitAsync(eventA.Task);
        var cardB = await WaitAsync(eventB.Task);

        Assert.Equal("Realtime Task", cardA.Title);
        Assert.Equal(cardA.Id, cardB.Id);
    }

    private async Task<ColumnDto> CreateColumnAsync(string title)
    {
        var response = await Client.PostAsJsonAsync("/api/columns", new CreateColumnRequest(title));
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>();
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        return envelope.Data!;
    }

    private static async Task StartConnectionsAsync(params HubConnection[] connections)
    {
        foreach (var connection in connections)
        {
            await connection.StartAsync();
        }
    }

    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
}
