using System.Net.Http.Json;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.CardType;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class RealtimeIntegrationTests : BoardApiIntegrationTestBase
{
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
        var columnId = await SeedBoardColumnAsync("Todo");

        await using var connectionA = CreateHubConnection();
        await using var connectionB = CreateHubConnection();

        var eventA = new TaskCompletionSource<CardDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var eventB = new TaskCompletionSource<CardDto>(TaskCreationOptions.RunContinuationsAsynchronously);

        connectionA.On<CardDto>("CardCreated", card => eventA.TrySetResult(card));
        connectionB.On<CardDto>("CardCreated", card => eventB.TrySetResult(card));

        await StartConnectionsAsync(1, connectionA, connectionB);

        // Act
        var createCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(columnId, "Realtime Task", "Desc", null));
        createCardResponse.EnsureSuccessStatusCode();

        // Assert
        var cardA = await WaitAsync(eventA.Task);
        var cardB = await WaitAsync(eventB.Task);

        Assert.Equal("Realtime Task", cardA.Title);
        Assert.Equal(cardA.Id, cardB.Id);
        Assert.True(cardA.CardTypeId > 0);
        Assert.Equal("Story", cardA.CardTypeName);
        Assert.Null(cardA.CardTypeEmoji);
    }

    [Fact]
    public async Task CardUpdated_ShouldBroadcastCardTypeFields()
    {
        // Arrange
        var columnId = await SeedBoardColumnAsync("Todo");
        var bugTypeId = await SeedBoardCardTypeAsync("Bug", emoji: "🐞");
        var cardId = await SeedBoardCardAsync(columnId, "Realtime Task", "Desc");

        await using var connection = CreateHubConnection();
        var updatedEvent = new TaskCompletionSource<CardDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<CardDto>("CardUpdated", card => updatedEvent.TrySetResult(card));
        await StartConnectionsAsync(1, connection);

        // Act
        var updateResponse = await Client.PutAsJsonAsync(
            $"/api/boards/1/cards/{cardId}",
            new UpdateCardRequest("Realtime Task", "Desc", [], bugTypeId));
        updateResponse.EnsureSuccessStatusCode();

        // Assert
        var updatedCard = await WaitAsync(updatedEvent.Task);
        Assert.Equal(cardId, updatedCard.Id);
        Assert.Equal(bugTypeId, updatedCard.CardTypeId);
        Assert.Equal("Bug", updatedCard.CardTypeName);
        Assert.Equal("🐞", updatedCard.CardTypeEmoji);
    }

    [Fact]
    public async Task CardTypeUpdated_ShouldRequestBoardResync()
    {
        var cardTypeId = await SeedBoardCardTypeAsync("Bug", emoji: "🐞");

        await using var connection = CreateHubConnection();
        var resyncEvent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On("ResyncRequested", () => resyncEvent.TrySetResult(true));
        await StartConnectionsAsync(1, connection);

        var updateTypeResponse = await Client.PutAsJsonAsync(
            $"/api/boards/1/card-types/{cardTypeId}",
            new UpdateCardTypeRequest("Defect", "⚠️"));
        updateTypeResponse.EnsureSuccessStatusCode();

        await WaitAsync(resyncEvent.Task);
    }

    [Fact]
    public async Task CardTypeDeleted_ShouldRequestBoardResync()
    {
        var cardTypeId = await SeedBoardCardTypeAsync("Bug", emoji: "🐞");

        await using var connection = CreateHubConnection();
        var resyncEvent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On("ResyncRequested", () => resyncEvent.TrySetResult(true));
        await StartConnectionsAsync(1, connection);

        var deleteTypeResponse = await Client.DeleteAsync($"/api/boards/1/card-types/{cardTypeId}");
        deleteTypeResponse.EnsureSuccessStatusCode();

        await WaitAsync(resyncEvent.Task);
    }

    private static async Task StartConnectionsAsync(int boardId, params HubConnection[] connections)
    {
        foreach (var connection in connections)
        {
            await connection.StartAsync();
            await connection.InvokeAsync("SubscribeBoard", boardId);
        }
    }
}
