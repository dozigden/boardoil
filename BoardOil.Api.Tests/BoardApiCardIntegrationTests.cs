using System.Net;
using System.Net.Http.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Column;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class BoardApiCardIntegrationTests
    : BoardApiIntegrationTestBase
{
    [Fact]
    public async Task CardEndpoints_ShouldCreateCard_WithTagNames()
    {
        // Arrange
        var createdColumnId = await SeedBoardColumnAsync("Todo");
        _ = await SeedBoardTagAsync("Bug");
        _ = await SeedBoardTagAsync("Urgent");

        // Act
        var createdCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(createdColumnId, "Task A", "Desc", ["Bug", "Urgent"]));
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
        var createdColumnId = await SeedBoardColumnAsync("Todo");
        var createdCardId = await SeedBoardCardAsync(createdColumnId, "Task A", "Desc");

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/1/cards/{createdCardId}",
            new
            {
                title = "Task A",
                description = "Desc",
                tagNames = Array.Empty<string>()
            });
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions);

        // Assert
        Assert.Equal(400, (int)response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal(400, payload.StatusCode);
    }

    [Fact]
    public async Task CardEndpoints_Move_ShouldReturnSuccessContract()
    {
        // Arrange
        var createdTodoColumnId = await SeedBoardColumnAsync("Todo");
        var createdDoingColumnId = await SeedBoardColumnAsync("Doing");
        var createdCardId = await SeedBoardCardAsync(createdTodoColumnId, "Task A", "Desc");

        // Act
        var movedCardResponse = await Client.PatchAsJsonAsync(
            $"/api/boards/1/cards/{createdCardId}/move",
            new MoveCardRequest(createdDoingColumnId, null));
        var movedCardEnvelope = await movedCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, movedCardResponse.StatusCode);
        Assert.NotNull(movedCardEnvelope);
        Assert.True(movedCardEnvelope!.Success);
        Assert.NotNull(movedCardEnvelope.Data);
        Assert.Equal(createdCardId, movedCardEnvelope.Data!.Id);
        Assert.Equal(createdDoingColumnId, movedCardEnvelope.Data.BoardColumnId);
    }

    [Fact]
    public async Task CardEndpoints_Archive_ShouldReturnSuccessContract()
    {
        // Arrange
        var createdColumnId = await SeedBoardColumnAsync("Todo");
        var createdCardId = await SeedBoardCardAsync(createdColumnId, "Archive me", "Desc");

        // Act
        var archiveResponse = await Client.PostAsync($"/api/boards/1/cards/{createdCardId}/archive", content: null);
        var archivedCardEnvelope = await archiveResponse.Content.ReadFromJsonAsync<ApiEnvelope<ArchivedCardDto>>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);
        Assert.NotNull(archivedCardEnvelope);
        Assert.True(archivedCardEnvelope!.Success);
        Assert.NotNull(archivedCardEnvelope!.Data);
        Assert.Equal(createdCardId, archivedCardEnvelope.Data!.OriginalCardId);
        Assert.True(archivedCardEnvelope.Data.Id > 0);
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
            await AuthenticateAsInitialAdminAsync(client, factory.Services);
            var create = await client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Persisted"));
            create.EnsureSuccessStatusCode();
        }

        await using (var factory = new BoardOilApiFactory(dbPath))
        {
            var client = factory.CreateClient();
            await AuthenticateAsInitialAdminAsync(client, factory.Services);
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
