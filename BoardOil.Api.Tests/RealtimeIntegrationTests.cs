using System.Net.Http.Json;
using BoardOil.Api.Realtime;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Services.Contracts;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class RealtimeIntegrationTests : TestBaseIntegration
{
    protected override string DbNamePrefix => "boardoil-realtime-tests";
    protected override int TypingTtlSeconds => 1;

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

    [Fact]
    public async Task TypingEvents_ShouldBroadcastStartAndExpiry()
    {
        // Arrange
        var column = await CreateColumnAsync("Todo");
        var card = await CreateCardAsync(column.Id, "Typing Task", "Desc");

        await using var connectionA = CreateHubConnection();
        await using var connectionB = CreateHubConnection();

        var started = new TaskCompletionSource<TypingChangedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var expired = new TaskCompletionSource<TypingChangedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

        connectionB.On<TypingChangedEvent>("TypingChanged", evt =>
        {
            if (evt.CardId != card.Id || evt.UserLabel != "UserA")
            {
                return;
            }

            if (evt.IsTyping)
            {
                started.TrySetResult(evt);
                return;
            }

            expired.TrySetResult(evt);
        });

        await StartConnectionsAsync(connectionA, connectionB);

        // Act
        await connectionA.InvokeAsync("TypingStarted", card.Id, "title", "UserA");

        // Assert
        var startedEvent = await WaitAsync(started.Task);
        Assert.True(startedEvent.IsTyping);

        var expiredEvent = await WaitAsync(expired.Task, TimeSpan.FromSeconds(5));
        Assert.False(expiredEvent.IsTyping);
    }

    private async Task<ColumnDto> CreateColumnAsync(string title)
    {
        var response = await Client.PostAsJsonAsync("/api/columns", new CreateColumnRequest(title, null));
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>();
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        return envelope.Data!;
    }

    private async Task<CardDto> CreateCardAsync(int columnId, string title, string description)
    {
        var response = await Client.PostAsJsonAsync(
            "/api/cards",
            new CreateCardRequest(columnId, title, description, null));
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>();
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
