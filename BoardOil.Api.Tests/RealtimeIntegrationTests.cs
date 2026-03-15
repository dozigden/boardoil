using System.Net.Http.Json;
using BoardOil.Api.Realtime;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Services.Contracts;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class RealtimeIntegrationTests
{
    [Fact]
    public async Task CardCreated_ShouldBroadcastToTwoConnectedClients()
    {
        var dbPath = NewDbPath();
        await using var factory = new BoardOilApiFactory(dbPath);
        var client = factory.CreateClient();

        var createColumnResponse = await client.PostAsJsonAsync("/api/columns", new CreateColumnRequest("Todo", null));
        createColumnResponse.EnsureSuccessStatusCode();
        var createColumnEnvelope = await createColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>();
        Assert.NotNull(createColumnEnvelope);
        Assert.NotNull(createColumnEnvelope!.Data);

        await using var connectionA = CreateHubConnection(factory);
        await using var connectionB = CreateHubConnection(factory);

        var eventA = new TaskCompletionSource<CardDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var eventB = new TaskCompletionSource<CardDto>(TaskCreationOptions.RunContinuationsAsynchronously);

        connectionA.On<CardDto>("CardCreated", card => eventA.TrySetResult(card));
        connectionB.On<CardDto>("CardCreated", card => eventB.TrySetResult(card));

        await connectionA.StartAsync();
        await connectionB.StartAsync();

        var createCardResponse = await client.PostAsJsonAsync(
            "/api/cards",
            new CreateCardRequest(createColumnEnvelope.Data!.Id, "Realtime Task", "Desc", null));
        createCardResponse.EnsureSuccessStatusCode();

        var cardA = await WaitAsync(eventA.Task);
        var cardB = await WaitAsync(eventB.Task);

        Assert.Equal("Realtime Task", cardA.Title);
        Assert.Equal(cardA.Id, cardB.Id);
    }

    [Fact]
    public async Task TypingEvents_ShouldBroadcastStartAndExpiry()
    {
        var dbPath = NewDbPath();
        await using var factory = new BoardOilApiFactory(dbPath, typingTtlSeconds: 1);
        var client = factory.CreateClient();

        var createColumnResponse = await client.PostAsJsonAsync("/api/columns", new CreateColumnRequest("Todo", null));
        createColumnResponse.EnsureSuccessStatusCode();
        var createColumnEnvelope = await createColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>();
        Assert.NotNull(createColumnEnvelope);

        var createCardResponse = await client.PostAsJsonAsync(
            "/api/cards",
            new CreateCardRequest(createColumnEnvelope!.Data!.Id, "Typing Task", "Desc", null));
        createCardResponse.EnsureSuccessStatusCode();
        var createCardEnvelope = await createCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>();
        Assert.NotNull(createCardEnvelope);

        await using var connectionA = CreateHubConnection(factory);
        await using var connectionB = CreateHubConnection(factory);

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

        await connectionA.InvokeAsync("TypingStarted", createCardEnvelope!.Data!.Id, "title", "UserA");

        var startedEvent = await WaitAsync(started.Task);
        Assert.True(startedEvent.IsTyping);

        var expiredEvent = await WaitAsync(expired.Task, TimeSpan.FromSeconds(5));
        Assert.False(expiredEvent.IsTyping);
    }

    private static HubConnection CreateHubConnection(BoardOilApiFactory factory)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress!, "/hubs/board"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }

    private static async Task<T> WaitAsync<T>(Task<T> task, TimeSpan? timeout = null)
    {
        var wait = timeout ?? TimeSpan.FromSeconds(3);
        var completed = await Task.WhenAny(task, Task.Delay(wait));
        if (completed != task)
        {
            throw new TimeoutException($"Timed out waiting for event after {wait}.");
        }

        return await task;
    }

    private static string NewDbPath()
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), ".test-data");
        Directory.CreateDirectory(root);
        return Path.Combine(root, $"boardoil-realtime-tests-{Guid.NewGuid():N}.db");
    }

    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
}
