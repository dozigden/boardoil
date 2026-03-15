using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Services.Contracts;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class BoardApiIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetBoard_ShouldReturnBootstrappedSingleBoard()
    {
        var dbPath = NewDbPath();
        await using var factory = new BoardOilApiFactory(dbPath);
        var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/board", JsonOptions);

        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("BoardOil", result.Data!.Name);
        Assert.Empty(result.Data.Columns);
    }

    [Fact]
    public async Task CardAndColumnEndpoints_ShouldApplyChangesEndToEnd()
    {
        var dbPath = NewDbPath();
        await using var factory = new BoardOilApiFactory(dbPath);
        var client = factory.CreateClient();

        var createdColumnResponse = await client.PostAsJsonAsync("/api/columns", new CreateColumnRequest("Todo", null));
        createdColumnResponse.EnsureSuccessStatusCode();
        var createdColumn = await createdColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(createdColumn);
        Assert.NotNull(createdColumn!.Data);

        var createdCardResponse = await client.PostAsJsonAsync(
            "/api/cards",
            new CreateCardRequest(createdColumn.Data!.Id, "Task A", "Desc", null));
        createdCardResponse.EnsureSuccessStatusCode();
        var createdCard = await createdCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);
        Assert.NotNull(createdCard);
        Assert.NotNull(createdCard!.Data);

        var updatedCardResponse = await client.PatchAsJsonAsync(
            $"/api/cards/{createdCard.Data!.Id}",
            new UpdateCardRequest(createdColumn.Data.Id, null, null, 0));
        updatedCardResponse.EnsureSuccessStatusCode();

        var board = await client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/board", JsonOptions);
        Assert.NotNull(board);
        Assert.NotNull(board!.Data);
        Assert.Single(board.Data!.Columns);
        Assert.Single(board.Data.Columns[0].Cards);
        Assert.Equal("Task A", board.Data.Columns[0].Cards[0].Title);

        var deleteCardResponse = await client.DeleteAsync($"/api/cards/{createdCard.Data.Id}");
        deleteCardResponse.EnsureSuccessStatusCode();
        var deleteColumnResponse = await client.DeleteAsync($"/api/columns/{createdColumn.Data.Id}");
        deleteColumnResponse.EnsureSuccessStatusCode();

        var afterDelete = await client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/board", JsonOptions);
        Assert.NotNull(afterDelete);
        Assert.NotNull(afterDelete!.Data);
        Assert.Empty(afterDelete.Data!.Columns);
    }

    [Fact]
    public async Task Data_ShouldPersistAcrossFactoryRestarts_WhenUsingSameDatabasePath()
    {
        var dbPath = NewDbPath();

        await using (var factory = new BoardOilApiFactory(dbPath))
        {
            var client = factory.CreateClient();
            var create = await client.PostAsJsonAsync("/api/columns", new CreateColumnRequest("Persisted", null));
            create.EnsureSuccessStatusCode();
        }

        await using (var factory = new BoardOilApiFactory(dbPath))
        {
            var client = factory.CreateClient();
            var board = await client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/board", JsonOptions);

            Assert.NotNull(board);
            Assert.NotNull(board!.Data);
            Assert.Single(board.Data!.Columns);
            Assert.Equal("Persisted", board.Data.Columns[0].Title);
        }
    }

    [Fact]
    public async Task DeleteCard_WhenMissing_ShouldReturnErrorContract()
    {
        var dbPath = NewDbPath();
        await using var factory = new BoardOilApiFactory(dbPath);
        var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/cards/999999");
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions);

        Assert.Equal(404, (int)response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal(404, payload.StatusCode);
        Assert.Equal("Card not found.", payload.Message);
    }

    private static string NewDbPath()
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), ".test-data");
        Directory.CreateDirectory(root);
        return Path.Combine(root, $"boardoil-api-tests-{Guid.NewGuid():N}.db");
    }

    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
}
