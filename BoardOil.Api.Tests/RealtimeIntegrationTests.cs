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
        var createColumnResponse = await Client.PostAsJsonAsync("/api/columns", new CreateColumnRequest("Todo", null));
        createColumnResponse.EnsureSuccessStatusCode();
        var createColumnEnvelope = await createColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>();
        Assert.NotNull(createColumnEnvelope);
        Assert.NotNull(createColumnEnvelope!.Data);

        await using var connectionA = CreateHubConnection();
        await using var connectionB = CreateHubConnection();

        var eventA = new TaskCompletionSource<CardDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var eventB = new TaskCompletionSource<CardDto>(TaskCreationOptions.RunContinuationsAsynchronously);

        connectionA.On<CardDto>("CardCreated", card => eventA.TrySetResult(card));
        connectionB.On<CardDto>("CardCreated", card => eventB.TrySetResult(card));

        await connectionA.StartAsync();
        await connectionB.StartAsync();

        // Act
        var createCardResponse = await Client.PostAsJsonAsync(
            "/api/cards",
            new CreateCardRequest(createColumnEnvelope.Data!.Id, "Realtime Task", "Desc", null));
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
        var createColumnResponse = await Client.PostAsJsonAsync("/api/columns", new CreateColumnRequest("Todo", null));
        createColumnResponse.EnsureSuccessStatusCode();
        var createColumnEnvelope = await createColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>();
        Assert.NotNull(createColumnEnvelope);

        var createCardResponse = await Client.PostAsJsonAsync(
            "/api/cards",
            new CreateCardRequest(createColumnEnvelope!.Data!.Id, "Typing Task", "Desc", null));
        createCardResponse.EnsureSuccessStatusCode();
        var createCardEnvelope = await createCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>();
        Assert.NotNull(createCardEnvelope);

        await using var connectionA = CreateHubConnection();
        await using var connectionB = CreateHubConnection();

        var started = new TaskCompletionSource<TypingChangedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var expired = new TaskCompletionSource<TypingChangedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

        connectionB.On<TypingChangedEvent>("TypingChanged", evt =>
        {
            if (evt.CardId != createCardEnvelope!.Data!.Id || evt.UserLabel != "UserA")
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

        await connectionA.StartAsync();
        await connectionB.StartAsync();

        // Act
        await connectionA.InvokeAsync("TypingStarted", createCardEnvelope!.Data!.Id, "title", "UserA");

        // Assert
        var startedEvent = await WaitAsync(started.Task);
        Assert.True(startedEvent.IsTyping);

        var expiredEvent = await WaitAsync(expired.Task, TimeSpan.FromSeconds(5));
        Assert.False(expiredEvent.IsTyping);
    }

    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
}
