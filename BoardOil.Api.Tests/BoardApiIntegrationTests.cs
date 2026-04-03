using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Column;
using BoardOil.Contracts.Tag;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class BoardApiIntegrationTests
    : TestBaseIntegration
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetBoard_ShouldReturnBootstrappedBoardWithDefaultColumns()
    {
        var result = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/boards/1", JsonOptions);

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
    public async Task GetBoards_ShouldReturnBoardList()
    {
        var result = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<BoardSummaryDto>>>("/api/boards", JsonOptions);

        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.NotNull(result.Data);
        Assert.NotEmpty(result.Data!);
        Assert.Contains(result.Data!, x => x.Name == "BoardOil");
    }

    [Fact]
    public async Task CreateBoard_ShouldAllowDuplicateNamesAndSeedDefaultColumns()
    {
        var firstCreateResponse = await Client.PostAsJsonAsync("/api/boards", new CreateBoardRequest("Roadmap"));
        firstCreateResponse.EnsureSuccessStatusCode();
        var first = await firstCreateResponse.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>(JsonOptions);
        Assert.NotNull(first);
        Assert.NotNull(first!.Data);

        var secondCreateResponse = await Client.PostAsJsonAsync("/api/boards", new CreateBoardRequest("Roadmap"));
        secondCreateResponse.EnsureSuccessStatusCode();
        var second = await secondCreateResponse.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>(JsonOptions);
        Assert.NotNull(second);
        Assert.NotNull(second!.Data);

        Assert.NotEqual(first.Data!.Id, second.Data!.Id);
        Assert.Equal(["Todo", "In Progress", "Done"], first.Data.Columns.Select(x => x.Title).ToArray());
        Assert.Equal(["Todo", "In Progress", "Done"], second.Data.Columns.Select(x => x.Title).ToArray());
    }

    [Fact]
    public async Task UpdateBoard_ShouldRenameBoard()
    {
        // Arrange
        var createResponse = await Client.PostAsJsonAsync("/api/boards", new CreateBoardRequest("Roadmap"));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>(JsonOptions);
        Assert.NotNull(created);
        Assert.NotNull(created!.Data);

        // Act
        var updateResponse = await Client.PutAsJsonAsync($"/api/boards/{created.Data!.Id}", new UpdateBoardRequest("  Product Roadmap  "));
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<ApiEnvelope<BoardSummaryDto>>(JsonOptions);

        // Assert
        Assert.NotNull(updated);
        Assert.NotNull(updated!.Data);
        Assert.Equal(created.Data.Id, updated.Data!.Id);
        Assert.Equal("Product Roadmap", updated.Data.Name);

        var boardList = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<BoardSummaryDto>>>("/api/boards", JsonOptions);
        Assert.NotNull(boardList);
        Assert.NotNull(boardList!.Data);
        Assert.Contains(boardList.Data!, x => x.Id == created.Data.Id && x.Name == "Product Roadmap");
    }

    [Fact]
    public async Task DeleteBoard_ShouldRemoveBoardAndChildren()
    {
        // Arrange
        var createBoardResponse = await Client.PostAsJsonAsync("/api/boards", new CreateBoardRequest("Disposable"));
        createBoardResponse.EnsureSuccessStatusCode();
        var createdBoard = await createBoardResponse.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>(JsonOptions);
        Assert.NotNull(createdBoard);
        Assert.NotNull(createdBoard!.Data);
        var boardId = createdBoard.Data!.Id;
        var todoColumnId = createdBoard.Data.Columns[0].Id;

        var createCardResponse = await Client.PostAsJsonAsync(
            $"/api/boards/{boardId}/cards",
            new CreateCardRequest(todoColumnId, "Card A", "Desc", null));
        createCardResponse.EnsureSuccessStatusCode();

        // Act
        var deleteBoardResponse = await Client.DeleteAsync($"/api/boards/{boardId}");
        deleteBoardResponse.EnsureSuccessStatusCode();

        // Assert
        var boardResponse = await Client.GetAsync($"/api/boards/{boardId}");
        var boardPayload = await boardResponse.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions);
        Assert.Equal(404, (int)boardResponse.StatusCode);
        Assert.NotNull(boardPayload);
        Assert.False(boardPayload!.Success);
        Assert.Equal("Board not found.", boardPayload.Message);

        var boardList = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<BoardSummaryDto>>>("/api/boards", JsonOptions);
        Assert.NotNull(boardList);
        Assert.NotNull(boardList!.Data);
        Assert.DoesNotContain(boardList.Data!, x => x.Id == boardId);
    }

    [Fact]
    public async Task DeleteBoard_WhenMissing_ShouldReturnOkContract()
    {
        // Act
        var response = await Client.DeleteAsync("/api/boards/999999");
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions);

        // Assert
        Assert.Equal(200, (int)response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.Success);
        Assert.Equal(200, payload.StatusCode);
        Assert.Null(payload.Message);
    }

    [Fact]
    public async Task ColumnEndpoints_ShouldCreateAndDeleteColumn()
    {
        // Arrange
        var createdColumnResponse = await Client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Todo"));
        createdColumnResponse.EnsureSuccessStatusCode();
        var createdColumn = await createdColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(createdColumn);
        Assert.NotNull(createdColumn!.Data);

        // Assert created
        var board = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/boards/1", JsonOptions);
        Assert.NotNull(board);
        Assert.NotNull(board!.Data);
        Assert.Equal(4, board.Data!.Columns.Count);
        Assert.Contains(board.Data.Columns, column => column.Id == createdColumn.Data.Id && column.Title == "Todo");

        // Act
        var deleteColumnResponse = await Client.DeleteAsync($"/api/boards/1/columns/{createdColumn.Data.Id}");
        deleteColumnResponse.EnsureSuccessStatusCode();

        // Assert deleted
        var afterDelete = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/boards/1", JsonOptions);
        Assert.NotNull(afterDelete);
        Assert.NotNull(afterDelete!.Data);
        Assert.Equal(3, afterDelete.Data!.Columns.Count);
        Assert.Equal(["Todo", "In Progress", "Done"], afterDelete.Data.Columns.Select(x => x.Title).ToArray());
    }

    [Fact]
    public async Task ColumnEndpoints_ShouldUpdateColumnTitle_WithPut()
    {
        // Arrange
        var createdColumnResponse = await Client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Todo"));
        createdColumnResponse.EnsureSuccessStatusCode();
        var createdColumn = await createdColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(createdColumn);
        Assert.NotNull(createdColumn!.Data);

        // Act
        var updateResponse = await Client.PutAsJsonAsync(
            $"/api/boards/1/columns/{createdColumn.Data!.Id}",
            new UpdateColumnRequest("  Updated Todo  "));
        updateResponse.EnsureSuccessStatusCode();

        // Assert
        var board = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/boards/1", JsonOptions);
        Assert.NotNull(board);
        Assert.NotNull(board!.Data);
        var updated = board.Data!.Columns.Single(x => x.Id == createdColumn.Data.Id);
        Assert.Equal("Updated Todo", updated.Title);
    }

    [Fact]
    public async Task ColumnEndpoints_ShouldMoveColumnToStart_WhenPositionAfterColumnIdIsNull()
    {
        // Arrange
        var createdFirstColumnResponse = await Client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("A"));
        createdFirstColumnResponse.EnsureSuccessStatusCode();
        var createdFirstColumn = await createdFirstColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(createdFirstColumn);
        Assert.NotNull(createdFirstColumn!.Data);

        var createdSecondColumnResponse = await Client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("B"));
        createdSecondColumnResponse.EnsureSuccessStatusCode();
        var createdSecondColumn = await createdSecondColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(createdSecondColumn);
        Assert.NotNull(createdSecondColumn!.Data);

        // Act
        var moveResponse = await Client.PatchAsJsonAsync(
            $"/api/boards/1/columns/{createdSecondColumn.Data!.Id}/move",
            new MoveColumnRequest(null));
        moveResponse.EnsureSuccessStatusCode();

        // Assert
        var board = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/boards/1", JsonOptions);
        Assert.NotNull(board);
        Assert.NotNull(board!.Data);
        Assert.Equal(createdSecondColumn.Data.Id, board.Data!.Columns[0].Id);
        Assert.Contains(board.Data.Columns, x => x.Id == createdFirstColumn.Data!.Id);
    }

    [Fact]
    public async Task StateChangingEndpoint_WhenCsrfHeaderMissing_ShouldReturnForbidden()
    {
        // Arrange
        Client.DefaultRequestHeaders.Remove("X-BoardOil-CSRF");

        // Act
        var response = await Client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Todo"));
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions);

        // Assert
        Assert.Equal(403, (int)response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal(403, payload.StatusCode);
        Assert.Equal("CSRF validation failed.", payload.Message);
    }

    [Fact]
    public async Task CardEndpoints_ShouldCreateCard_WithTagNames()
    {
        // Arrange
        var createdColumnResponse = await Client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Todo"));
        createdColumnResponse.EnsureSuccessStatusCode();
        var createdColumn = await createdColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(createdColumn);
        Assert.NotNull(createdColumn!.Data);
        var createBugTagResponse = await Client.PostAsJsonAsync("/api/boards/1/tags", new CreateTagRequest("Bug"));
        createBugTagResponse.EnsureSuccessStatusCode();
        var createUrgentTagResponse = await Client.PostAsJsonAsync("/api/boards/1/tags", new CreateTagRequest("Urgent"));
        createUrgentTagResponse.EnsureSuccessStatusCode();

        // Act
        var createdCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(createdColumn.Data!.Id, "Task A", "Desc", ["Bug", "Urgent"]));
        createdCardResponse.EnsureSuccessStatusCode();
        var createdCard = await createdCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);

        // Assert
        Assert.NotNull(createdCard);
        Assert.NotNull(createdCard!.Data);
        Assert.Equal("Task A", createdCard.Data!.Title);
        Assert.Equal(["Bug", "Urgent"], createdCard.Data.TagNames);
    }

    [Fact]
    public async Task CardEndpoints_ShouldUpdateCard_TitleAndTagNames()
    {
        // Arrange
        var createdColumnResponse = await Client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Todo"));
        createdColumnResponse.EnsureSuccessStatusCode();
        var createdColumn = await createdColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(createdColumn);
        Assert.NotNull(createdColumn!.Data);
        var createBugTagResponse = await Client.PostAsJsonAsync("/api/boards/1/tags", new CreateTagRequest("Bug"));
        createBugTagResponse.EnsureSuccessStatusCode();
        var createUrgentTagResponse = await Client.PostAsJsonAsync("/api/boards/1/tags", new CreateTagRequest("Urgent"));
        createUrgentTagResponse.EnsureSuccessStatusCode();

        var createdCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(createdColumn.Data!.Id, "Task A", "Desc", ["Bug", "Urgent"]));
        createdCardResponse.EnsureSuccessStatusCode();
        var createdCard = await createdCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);
        Assert.NotNull(createdCard);
        Assert.NotNull(createdCard!.Data);

        // Act
        var updatedCardResponse = await Client.PutAsJsonAsync(
            $"/api/boards/1/cards/{createdCard.Data!.Id}",
            new UpdateCardRequest("Task B", "Desc", ["Urgent"]));
        updatedCardResponse.EnsureSuccessStatusCode();

        // Assert
        var board = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/boards/1", JsonOptions);
        Assert.NotNull(board);
        Assert.NotNull(board!.Data);
        var createdColumnState = board.Data.Columns.FirstOrDefault(x => x.Id == createdColumn.Data.Id);
        Assert.NotNull(createdColumnState);
        Assert.Single(createdColumnState!.Cards);
        Assert.Equal("Task B", createdColumnState.Cards[0].Title);
        Assert.Equal(["Urgent"], createdColumnState.Cards[0].TagNames);
    }

    [Fact]
    public async Task CardEndpoints_ShouldMoveCard()
    {
        // Arrange
        var createdTodoColumnResponse = await Client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Todo"));
        createdTodoColumnResponse.EnsureSuccessStatusCode();
        var createdTodoColumn = await createdTodoColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(createdTodoColumn);
        Assert.NotNull(createdTodoColumn!.Data);

        var createdDoingColumnResponse = await Client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Doing"));
        createdDoingColumnResponse.EnsureSuccessStatusCode();
        var createdDoingColumn = await createdDoingColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(createdDoingColumn);
        Assert.NotNull(createdDoingColumn!.Data);

        var createdCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(createdTodoColumn.Data!.Id, "Task A", "Desc", null));
        createdCardResponse.EnsureSuccessStatusCode();
        var createdCard = await createdCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);
        Assert.NotNull(createdCard);
        Assert.NotNull(createdCard!.Data);

        // Act
        var movedCardResponse = await Client.PatchAsJsonAsync(
            $"/api/boards/1/cards/{createdCard.Data!.Id}/move",
            new MoveCardRequest(createdDoingColumn.Data!.Id, null));
        movedCardResponse.EnsureSuccessStatusCode();

        // Assert
        var board = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/boards/1", JsonOptions);
        Assert.NotNull(board);
        Assert.NotNull(board!.Data);

        var todoState = board.Data.Columns.FirstOrDefault(x => x.Id == createdTodoColumn.Data.Id);
        var doingState = board.Data.Columns.FirstOrDefault(x => x.Id == createdDoingColumn.Data.Id);

        Assert.NotNull(todoState);
        Assert.NotNull(doingState);
        Assert.Empty(todoState!.Cards);
        Assert.Single(doingState!.Cards);
        Assert.Equal("Task A", doingState.Cards[0].Title);
    }

    [Fact]
    public async Task CardEndpoints_ShouldMoveCardToStart_WhenPositionAfterCardIdIsNullAndTargetHasCards()
    {
        // Arrange
        var createdTodoColumnResponse = await Client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Todo"));
        createdTodoColumnResponse.EnsureSuccessStatusCode();
        var createdTodoColumn = await createdTodoColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(createdTodoColumn);
        Assert.NotNull(createdTodoColumn!.Data);

        var createdDoingColumnResponse = await Client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Doing"));
        createdDoingColumnResponse.EnsureSuccessStatusCode();
        var createdDoingColumn = await createdDoingColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(createdDoingColumn);
        Assert.NotNull(createdDoingColumn!.Data);

        var createdSourceCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(createdTodoColumn.Data!.Id, "Move me", "Desc", null));
        createdSourceCardResponse.EnsureSuccessStatusCode();
        var createdSourceCard = await createdSourceCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);
        Assert.NotNull(createdSourceCard);
        Assert.NotNull(createdSourceCard!.Data);

        var existingFirstCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(createdDoingColumn.Data!.Id, "Existing A", "Desc", null));
        existingFirstCardResponse.EnsureSuccessStatusCode();

        var existingSecondCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(createdDoingColumn.Data!.Id, "Existing B", "Desc", null));
        existingSecondCardResponse.EnsureSuccessStatusCode();

        // Act
        var movedCardResponse = await Client.PatchAsJsonAsync(
            $"/api/boards/1/cards/{createdSourceCard.Data!.Id}/move",
            new MoveCardRequest(createdDoingColumn.Data!.Id, null));
        movedCardResponse.EnsureSuccessStatusCode();

        // Assert
        var board = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/boards/1", JsonOptions);
        Assert.NotNull(board);
        Assert.NotNull(board!.Data);

        var todoState = board.Data.Columns.FirstOrDefault(x => x.Id == createdTodoColumn.Data.Id);
        var doingState = board.Data.Columns.FirstOrDefault(x => x.Id == createdDoingColumn.Data.Id);

        Assert.NotNull(todoState);
        Assert.NotNull(doingState);
        Assert.Empty(todoState!.Cards);
        Assert.Equal(["Move me", "Existing A", "Existing B"], doingState!.Cards.Select(x => x.Title).ToArray());
    }

    [Fact]
    public async Task CardEndpoints_ShouldDeleteCard()
    {
        // Arrange
        var createdColumnResponse = await Client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Todo"));
        createdColumnResponse.EnsureSuccessStatusCode();
        var createdColumn = await createdColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(createdColumn);
        Assert.NotNull(createdColumn!.Data);
        var createBugTagResponse = await Client.PostAsJsonAsync("/api/boards/1/tags", new CreateTagRequest("Bug"));
        createBugTagResponse.EnsureSuccessStatusCode();

        var createdCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(createdColumn.Data!.Id, "Task A", "Desc", ["Bug"]));
        createdCardResponse.EnsureSuccessStatusCode();
        var createdCard = await createdCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);
        Assert.NotNull(createdCard);
        Assert.NotNull(createdCard!.Data);

        // Act
        var deleteCardResponse = await Client.DeleteAsync($"/api/boards/1/cards/{createdCard.Data.Id}");
        deleteCardResponse.EnsureSuccessStatusCode();

        // Assert
        var afterDelete = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/boards/1", JsonOptions);
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
            var create = await client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Persisted"));
            create.EnsureSuccessStatusCode();
        }

        await using (var factory = new BoardOilApiFactory(dbPath))
        {
            var client = factory.CreateClient();
            await AuthenticateAsInitialAdminAsync(client);
            var board = await client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/boards/1", JsonOptions);

            Assert.NotNull(board);
            Assert.NotNull(board!.Data);
            Assert.Equal(4, board.Data!.Columns.Count);
            Assert.Contains(board.Data.Columns, x => x.Title == "Persisted");
        }
    }

    [Fact]
    public async Task DeleteCard_WhenMissing_ShouldReturnOkContract()
    {
        var response = await Client.DeleteAsync("/api/boards/1/cards/999999");
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
        var response = await Client.DeleteAsync("/api/boards/1/columns/999999");
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions);

        Assert.Equal(200, (int)response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.Success);
        Assert.Equal(200, payload.StatusCode);
        Assert.Null(payload.Message);
    }

    [Fact]
    public async Task TagEndpoints_ShouldCreateTag()
    {
        // Arrange
        var request = new CreateTagRequest("Bug", "🐞");

        // Act
        var createResponse = await Client.PostAsJsonAsync("/api/boards/1/tags", request);
        createResponse.EnsureSuccessStatusCode();
        var createdTagEnvelope = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<TagDto>>(JsonOptions);

        // Assert
        Assert.NotNull(createdTagEnvelope);
        Assert.NotNull(createdTagEnvelope!.Data);
        Assert.Equal("Bug", createdTagEnvelope.Data!.Name);
        Assert.Equal("🐞", createdTagEnvelope.Data.Emoji);
        Assert.Equal(201, createdTagEnvelope.StatusCode);
    }

    [Fact]
    public async Task TagEndpoints_ShouldListTags()
    {
        // Arrange
        await SeedTagAsync("Bug", "BUG", "solid", """{"backgroundColor":"#224466","textColorMode":"auto"}""");

        // Act
        var initialTagsResponse = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<TagDto>>>("/api/boards/1/tags", JsonOptions);

        // Assert
        Assert.NotNull(initialTagsResponse);
        Assert.NotNull(initialTagsResponse!.Data);
        Assert.Contains(initialTagsResponse.Data!, x => x.Name == "Bug");
    }

    [Fact]
    public async Task TagEndpoints_ShouldUpdateTagStyles()
    {
        // Arrange
        await SeedTagAsync("Bug", "BUG", "solid", """{"backgroundColor":"#224466","textColorMode":"auto"}""");
        var request = new UpdateTagStyleRequest("Bug", "gradient", """{"leftColor":"#223344","rightColor":"#446688","textColorMode":"auto"}""", "⚠️");
        var tagsEnvelope = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<TagDto>>>("/api/boards/1/tags", JsonOptions);
        Assert.NotNull(tagsEnvelope);
        Assert.NotNull(tagsEnvelope!.Data);
        var bugTag = Assert.Single(tagsEnvelope.Data!, x => x.Name == "Bug");

        // Act
        var putResponse = await Client.PutAsJsonAsync(
            $"/api/boards/1/tags/{bugTag.Id}",
            request);
        putResponse.EnsureSuccessStatusCode();

        // Assert
        var patchedTagEnvelope = await putResponse.Content.ReadFromJsonAsync<ApiEnvelope<TagDto>>(JsonOptions);
        Assert.NotNull(patchedTagEnvelope);
        Assert.NotNull(patchedTagEnvelope!.Data);
        Assert.Equal("gradient", patchedTagEnvelope.Data!.StyleName);
        Assert.Equal("⚠️", patchedTagEnvelope.Data.Emoji);
    }

    [Fact]
    public async Task TagEndpoints_ShouldUpdateTagName()
    {
        // Arrange
        await SeedTagAsync("Bug", "BUG", "solid", """{"backgroundColor":"#224466","textColorMode":"auto"}""");
        var tagsEnvelope = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<TagDto>>>("/api/boards/1/tags", JsonOptions);
        Assert.NotNull(tagsEnvelope);
        Assert.NotNull(tagsEnvelope!.Data);
        var bugTag = Assert.Single(tagsEnvelope.Data!, x => x.Name == "Bug");

        var request = new UpdateTagStyleRequest(
            "Platform",
            "solid",
            """{"backgroundColor":"#224466","textColorMode":"auto"}""");

        // Act
        var putResponse = await Client.PutAsJsonAsync($"/api/boards/1/tags/{bugTag.Id}", request);
        putResponse.EnsureSuccessStatusCode();

        // Assert
        var updatedTagEnvelope = await putResponse.Content.ReadFromJsonAsync<ApiEnvelope<TagDto>>(JsonOptions);
        Assert.NotNull(updatedTagEnvelope);
        Assert.NotNull(updatedTagEnvelope!.Data);
        Assert.Equal("Platform", updatedTagEnvelope.Data!.Name);
    }

    [Fact]
    public async Task TagEndpoints_WhenEmojiOmitted_ShouldClearExistingEmoji()
    {
        // Arrange
        var createResponse = await Client.PostAsJsonAsync("/api/boards/1/tags", new CreateTagRequest("Bug", "🔥"));
        createResponse.EnsureSuccessStatusCode();
        var createdTagEnvelope = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<TagDto>>(JsonOptions);
        Assert.NotNull(createdTagEnvelope);
        Assert.NotNull(createdTagEnvelope!.Data);
        var bugTagId = createdTagEnvelope.Data!.Id;

        var request = new UpdateTagStyleRequest(
            "Bug",
            "solid",
            """{"backgroundColor":"#223344","textColorMode":"auto"}""");

        // Act
        var putResponse = await Client.PutAsJsonAsync($"/api/boards/1/tags/{bugTagId}", request);
        putResponse.EnsureSuccessStatusCode();

        // Assert
        var patchedTagEnvelope = await putResponse.Content.ReadFromJsonAsync<ApiEnvelope<TagDto>>(JsonOptions);
        Assert.NotNull(patchedTagEnvelope);
        Assert.NotNull(patchedTagEnvelope!.Data);
        Assert.Null(patchedTagEnvelope.Data!.Emoji);
    }

    [Fact]
    public async Task TagEndpoints_WhenTagIdMissing_ShouldReturnNotFound()
    {
        // Arrange
        var request = new UpdateTagStyleRequest("Bug", "solid", """{"backgroundColor":"#223344","textColorMode":"auto"}""");

        // Act
        var response = await Client.PutAsJsonAsync("/api/boards/1/tags/999999", request);
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions);

        // Assert
        Assert.Equal(404, (int)response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal(404, payload.StatusCode);
        Assert.Equal("Tag not found.", payload.Message);
    }

    [Fact]
    public async Task TagEndpoints_ShouldDeleteTag_AndRemoveTagFromExistingCards()
    {
        // Arrange
        var createdColumnResponse = await Client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Todo"));
        createdColumnResponse.EnsureSuccessStatusCode();
        var createdColumn = await createdColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(createdColumn);
        Assert.NotNull(createdColumn!.Data);

        var createdCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(createdColumn.Data!.Id, "Task A", "Desc", ["Bug", "Urgent"]));
        createdCardResponse.EnsureSuccessStatusCode();
        var createdCard = await createdCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);
        Assert.NotNull(createdCard);
        Assert.NotNull(createdCard!.Data);

        var tagsEnvelope = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<TagDto>>>("/api/boards/1/tags", JsonOptions);
        Assert.NotNull(tagsEnvelope);
        Assert.NotNull(tagsEnvelope!.Data);
        var bugTag = Assert.Single(tagsEnvelope.Data!, x => x.Name == "Bug");

        // Act
        var deleteResponse = await Client.DeleteAsync($"/api/boards/1/tags/{bugTag.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        // Assert
        var tagsAfterDelete = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<TagDto>>>("/api/boards/1/tags", JsonOptions);
        Assert.NotNull(tagsAfterDelete);
        Assert.NotNull(tagsAfterDelete!.Data);
        Assert.DoesNotContain(tagsAfterDelete.Data!, x => x.Name == "Bug");
        Assert.Contains(tagsAfterDelete.Data!, x => x.Name == "Urgent");

        var boardAfterDelete = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/boards/1", JsonOptions);
        Assert.NotNull(boardAfterDelete);
        Assert.NotNull(boardAfterDelete!.Data);
        var columnState = boardAfterDelete.Data!.Columns.Single(x => x.Id == createdColumn.Data.Id);
        var cardState = columnState.Cards.Single(x => x.Id == createdCard.Data!.Id);
        Assert.Equal("Task A", cardState.Title);
        Assert.Equal(["Urgent"], cardState.TagNames);
    }

    [Fact]
    public async Task DeleteTag_WhenMissing_ShouldReturnOkContract()
    {
        // Act
        var response = await Client.DeleteAsync("/api/boards/1/tags/999999");
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions);

        // Assert
        Assert.Equal(200, (int)response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.Success);
        Assert.Equal(200, payload.StatusCode);
        Assert.Null(payload.Message);
    }

    private async Task SeedTagAsync(string name, string normalisedName, string styleName, string stylePropertiesJson)
    {
        await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "Tags" ("BoardId", "Name", "NormalisedName", "StyleName", "StylePropertiesJson", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ($boardId, $name, $normalisedName, $styleName, $stylePropertiesJson, $createdAtUtc, $updatedAtUtc);
            """;
        command.Parameters.AddWithValue("$boardId", 1);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$normalisedName", normalisedName);
        command.Parameters.AddWithValue("$styleName", styleName);
        command.Parameters.AddWithValue("$stylePropertiesJson", stylePropertiesJson);
        var now = DateTime.UtcNow;
        command.Parameters.AddWithValue("$createdAtUtc", now);
        command.Parameters.AddWithValue("$updatedAtUtc", now);
        await command.ExecuteNonQueryAsync();
    }

    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
}
