using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Column;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class BoardApiIntegrationTests
    : TestBaseIntegration
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetBoard_ShouldReturnBootstrappedBoardWithDefaultColumns()
    {
        var result = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/board", JsonOptions);

        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("BoardOil", result.Data!.Name);
        Assert.Equal(3, result.Data.Columns.Count);
        Assert.Equal("Todo", result.Data.Columns[0].Title);
        Assert.Equal("In Progress", result.Data.Columns[1].Title);
        Assert.Equal("Done", result.Data.Columns[2].Title);
    }

    [Fact]
    public async Task ColumnEndpoints_ShouldCreateAndDeleteColumn()
    {
        // Arrange
        var createdColumnResponse = await Client.PostAsJsonAsync("/api/columns", new CreateColumnRequest("Todo", null));
        createdColumnResponse.EnsureSuccessStatusCode();
        var createdColumn = await createdColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(createdColumn);
        Assert.NotNull(createdColumn!.Data);

        // Assert created
        var board = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/board", JsonOptions);
        Assert.NotNull(board);
        Assert.NotNull(board!.Data);
        Assert.Equal(4, board.Data!.Columns.Count);
        Assert.Contains(board.Data.Columns, column => column.Id == createdColumn.Data.Id && column.Title == "Todo");

        // Act
        var deleteColumnResponse = await Client.DeleteAsync($"/api/columns/{createdColumn.Data.Id}");
        deleteColumnResponse.EnsureSuccessStatusCode();

        // Assert deleted
        var afterDelete = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/board", JsonOptions);
        Assert.NotNull(afterDelete);
        Assert.NotNull(afterDelete!.Data);
        Assert.Equal(3, afterDelete.Data!.Columns.Count);
        Assert.Equal(["Todo", "In Progress", "Done"], afterDelete.Data.Columns.Select(x => x.Title).ToArray());
    }

    [Fact]
    public async Task StateChangingEndpoint_WhenCsrfHeaderMissing_ShouldReturnForbidden()
    {
        // Arrange
        Client.DefaultRequestHeaders.Remove("X-BoardOil-CSRF");

        // Act
        var response = await Client.PostAsJsonAsync("/api/columns", new CreateColumnRequest("Todo", null));
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions);

        // Assert
        Assert.Equal(403, (int)response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal(403, payload.StatusCode);
        Assert.Equal("CSRF validation failed.", payload.Message);
    }

    [Fact]
    public async Task CardEndpoints_ShouldCreateUpdateAndDeleteCard()
    {
        // Arrange
        var createdColumnResponse = await Client.PostAsJsonAsync("/api/columns", new CreateColumnRequest("Todo", null));
        createdColumnResponse.EnsureSuccessStatusCode();
        var createdColumn = await createdColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(createdColumn);
        Assert.NotNull(createdColumn!.Data);

        // Act create
        var createdCardResponse = await Client.PostAsJsonAsync(
            "/api/cards",
            new CreateCardRequest(createdColumn.Data!.Id, "Task A", "Desc", null));
        createdCardResponse.EnsureSuccessStatusCode();
        var createdCard = await createdCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);
        Assert.NotNull(createdCard);
        Assert.NotNull(createdCard!.Data);

        // Act update
        var updatedCardResponse = await Client.PatchAsJsonAsync(
            $"/api/cards/{createdCard.Data!.Id}",
            new UpdateCardRequest(createdColumn.Data.Id, "Task B", null, 0));
        updatedCardResponse.EnsureSuccessStatusCode();

        // Assert updated
        var board = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/board", JsonOptions);
        Assert.NotNull(board);
        Assert.NotNull(board!.Data);
        var createdColumnState = board.Data.Columns.FirstOrDefault(x => x.Id == createdColumn.Data.Id);
        Assert.NotNull(createdColumnState);
        Assert.Single(createdColumnState!.Cards);
        Assert.Equal("Task B", createdColumnState.Cards[0].Title);

        // Act delete
        var deleteCardResponse = await Client.DeleteAsync($"/api/cards/{createdCard.Data.Id}");
        deleteCardResponse.EnsureSuccessStatusCode();

        // Assert deleted
        var afterDelete = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/board", JsonOptions);
        Assert.NotNull(afterDelete);
        Assert.NotNull(afterDelete!.Data);
        var columnAfterDelete = afterDelete.Data!.Columns.FirstOrDefault(x => x.Id == createdColumn.Data.Id);
        Assert.NotNull(columnAfterDelete);
        Assert.Empty(columnAfterDelete!.Cards);
    }

    [Fact]
    public async Task Data_ShouldPersistAcrossFactoryRestarts_WhenUsingSameDatabasePath()
    {
        var dbPath = CreateDbPath("boardoil-api-persist-tests");

        await using (var factory = new BoardOilApiFactory(dbPath))
        {
            var client = factory.CreateClient();
            await AuthenticateAsInitialAdminAsync(client);
            var create = await client.PostAsJsonAsync("/api/columns", new CreateColumnRequest("Persisted", null));
            create.EnsureSuccessStatusCode();
        }

        await using (var factory = new BoardOilApiFactory(dbPath))
        {
            var client = factory.CreateClient();
            await AuthenticateAsInitialAdminAsync(client);
            var board = await client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/board", JsonOptions);

            Assert.NotNull(board);
            Assert.NotNull(board!.Data);
            Assert.Equal(4, board.Data!.Columns.Count);
            Assert.Contains(board.Data.Columns, x => x.Title == "Persisted");
        }
    }

    [Fact]
    public async Task DeleteCard_WhenMissing_ShouldReturnOkContract()
    {
        var response = await Client.DeleteAsync("/api/cards/999999");
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions);

        Assert.Equal(200, (int)response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.Success);
        Assert.Equal(200, payload.StatusCode);
        Assert.Null(payload.Message);
    }

    [Fact]
    public async Task DeleteColumn_WhenMissing_ShouldReturnOkContract()
    {
        var response = await Client.DeleteAsync("/api/columns/999999");
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions);

        Assert.Equal(200, (int)response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.Success);
        Assert.Equal(200, payload.StatusCode);
        Assert.Null(payload.Message);
    }

    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
}
