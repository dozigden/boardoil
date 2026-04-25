using System.Net.Http.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.CardType;
using BoardOil.Contracts.Column;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class RealtimeIntegrationTests : TestBaseIntegration
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
        var column = await CreateColumnAsync("Todo");

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
            new CreateCardRequest(column.Id, "Realtime Task", "Desc", null));
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
        var column = await CreateColumnAsync("Todo");

        var createTypeResponse = await Client.PostAsJsonAsync("/api/boards/1/card-types", new CreateCardTypeRequest("Bug", "🐞"));
        createTypeResponse.EnsureSuccessStatusCode();
        var createdTypeEnvelope = await createTypeResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardTypeDto>>();
        Assert.NotNull(createdTypeEnvelope);
        Assert.NotNull(createdTypeEnvelope!.Data);
        var bugTypeId = createdTypeEnvelope.Data!.Id;

        var createCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(column.Id, "Realtime Task", "Desc", null));
        createCardResponse.EnsureSuccessStatusCode();
        var createdCardEnvelope = await createCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>();
        Assert.NotNull(createdCardEnvelope);
        Assert.NotNull(createdCardEnvelope!.Data);
        var cardId = createdCardEnvelope.Data!.Id;

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
        var createTypeResponse = await Client.PostAsJsonAsync("/api/boards/1/card-types", new CreateCardTypeRequest("Bug", "🐞"));
        createTypeResponse.EnsureSuccessStatusCode();
        var createdTypeEnvelope = await createTypeResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardTypeDto>>();
        Assert.NotNull(createdTypeEnvelope);
        Assert.NotNull(createdTypeEnvelope!.Data);

        await using var connection = CreateHubConnection();
        var resyncEvent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On("ResyncRequested", () => resyncEvent.TrySetResult(true));
        await StartConnectionsAsync(1, connection);

        var updateTypeResponse = await Client.PutAsJsonAsync(
            $"/api/boards/1/card-types/{createdTypeEnvelope.Data!.Id}",
            new UpdateCardTypeRequest("Defect", "⚠️"));
        updateTypeResponse.EnsureSuccessStatusCode();

        await WaitAsync(resyncEvent.Task);
    }

    [Fact]
    public async Task CardTypeDeleted_ShouldRequestBoardResync()
    {
        var createTypeResponse = await Client.PostAsJsonAsync("/api/boards/1/card-types", new CreateCardTypeRequest("Bug", "🐞"));
        createTypeResponse.EnsureSuccessStatusCode();
        var createdTypeEnvelope = await createTypeResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardTypeDto>>();
        Assert.NotNull(createdTypeEnvelope);
        Assert.NotNull(createdTypeEnvelope!.Data);

        await using var connection = CreateHubConnection();
        var resyncEvent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On("ResyncRequested", () => resyncEvent.TrySetResult(true));
        await StartConnectionsAsync(1, connection);

        var deleteTypeResponse = await Client.DeleteAsync($"/api/boards/1/card-types/{createdTypeEnvelope.Data!.Id}");
        deleteTypeResponse.EnsureSuccessStatusCode();

        await WaitAsync(resyncEvent.Task);
    }

    private async Task<ColumnDto> CreateColumnAsync(string title)
    {
        var response = await Client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest(title));
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>();
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        return envelope.Data!;
    }

    private static async Task StartConnectionsAsync(int boardId, params HubConnection[] connections)
    {
        foreach (var connection in connections)
        {
            await connection.StartAsync();
            await connection.InvokeAsync("SubscribeBoard", boardId);
        }
    }

    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
}
