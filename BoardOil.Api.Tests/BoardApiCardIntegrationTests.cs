using System.Net;
using System.Net.Http.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Column;
using BoardOil.Contracts.Tag;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class BoardApiCardIntegrationTests
    : BoardApiIntegrationTestBase
{
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
        Assert.Equal(["Bug", "Urgent"], createdCard.Data.Tags.Select(x => x.Name).ToArray());
        Assert.Equal(["Bug", "Urgent"], createdCard.Data.TagNames);
        Assert.True(createdCard.Data.CardTypeId > 0);
        Assert.Equal("Story", createdCard.Data.CardTypeName);
        Assert.Null(createdCard.Data.CardTypeEmoji);
    }

    [Fact]
    public async Task CardEndpoints_UpdateWithoutCardTypeId_ShouldReturnValidationError()
    {
        // Arrange
        var createdColumnResponse = await Client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Todo"));
        createdColumnResponse.EnsureSuccessStatusCode();
        var createdColumn = await createdColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(createdColumn);
        Assert.NotNull(createdColumn!.Data);

        var createdCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(createdColumn.Data!.Id, "Task A", "Desc", ["Bug"]));
        createdCardResponse.EnsureSuccessStatusCode();
        var createdCard = await createdCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);
        Assert.NotNull(createdCard);
        Assert.NotNull(createdCard!.Data);

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/1/cards/{createdCard.Data!.Id}",
            new
            {
                title = "Task A",
                description = "Desc",
                tagNames = new[] { "Bug" }
            });
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions);

        // Assert
        Assert.Equal(400, (int)response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal(400, payload.StatusCode);
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
    public async Task CardEndpoints_ShouldArchiveCard()
    {
        // Arrange
        var createdColumnResponse = await Client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Todo"));
        createdColumnResponse.EnsureSuccessStatusCode();
        var createdColumn = await createdColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(createdColumn);
        Assert.NotNull(createdColumn!.Data);
        var createTagResponse = await Client.PostAsJsonAsync("/api/boards/1/tags", new CreateTagRequest("Bug"));
        createTagResponse.EnsureSuccessStatusCode();
        var createdCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(createdColumn.Data!.Id, "Archive me", "Desc", ["Bug"]));
        createdCardResponse.EnsureSuccessStatusCode();
        var createdCard = await createdCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);
        Assert.NotNull(createdCard);
        Assert.NotNull(createdCard!.Data);

        // Act
        var archiveResponse = await Client.PostAsync($"/api/boards/1/cards/{createdCard.Data!.Id}/archive", content: null);
        archiveResponse.EnsureSuccessStatusCode();
        var archivedCardEnvelope = await archiveResponse.Content.ReadFromJsonAsync<ApiEnvelope<ArchivedCardDto>>(JsonOptions);

        // Assert
        Assert.NotNull(archivedCardEnvelope);
        Assert.NotNull(archivedCardEnvelope!.Data);
        Assert.Equal(createdCard.Data.Id, archivedCardEnvelope.Data!.OriginalCardId);
        Assert.Equal("Archive me", archivedCardEnvelope.Data.Title);
    }

    [Fact]
    public async Task CardEndpoints_GetArchivedById_WhenMissing_ShouldReturnNotFound()
    {
        // Act
        var response = await Client.GetAsync("/api/boards/1/cards/archived/999999");
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<ArchivedCardDetailDto>>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal(404, payload.StatusCode);
        Assert.Equal("Archived card not found.", payload.Message);
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

}
