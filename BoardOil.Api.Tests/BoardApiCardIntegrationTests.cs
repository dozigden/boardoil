using System.Net;
using System.Net.Http.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Column;
using BoardOil.Contracts.Slick;
using Microsoft.EntityFrameworkCore;
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
    public async Task CardEndpoints_WithConcurrentCreates_ShouldAtomicallyAllocateDistinctBoardCardIds()
    {
        // Arrange
        const int cardCount = 8;
        var columnIds = new List<int>(cardCount);
        for (var index = 0; index < cardCount; index++)
        {
            columnIds.Add(await SeedBoardColumnAsync($"Concurrent {index + 1}"));
        }

        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var createTasks = columnIds
            .Select(async (columnId, index) =>
            {
                await startGate.Task;
                return await Client.PostAsJsonAsync(
                    "/api/boards/1/cards",
                    new CreateCardRequest(columnId, $"Concurrent card {index + 1}", "", []));
            })
            .ToList();

        // Act
        startGate.SetResult();
        var responses = await Task.WhenAll(createTasks);
        var envelopes = new List<ApiEnvelope<CardDto>>(cardCount);
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);
            envelopes.Add(Assert.IsType<ApiEnvelope<CardDto>>(envelope));
        }

        // Assert
        var boardCardIds = envelopes
            .Select(x => Assert.IsType<CardDto>(x.Data).Id)
            .OrderBy(x => x)
            .ToArray();
        Assert.Equal(Enumerable.Range(1, cardCount), boardCardIds);

        await UseDbContextAsync(async dbContext =>
        {
            var persistedIds = await dbContext.Cards
                .Where(x => x.BoardId == 1)
                .OrderBy(x => x.BoardCardId)
                .Select(x => x.BoardCardId)
                .ToListAsync();
            Assert.Equal(Enumerable.Range(1, cardCount).Select(x => (int?)x), persistedIds);

            var nextCardId = await dbContext.BoardCardIdSequences
                .Where(x => x.BoardId == 1)
                .Select(x => x.NextCardId)
                .SingleAsync();
            Assert.Equal(cardCount + 1, nextCardId);
        });
    }

    [Fact]
    public async Task CardEndpoints_WhenBoardsShareCardId_ShouldKeepMutationsBoardScoped()
    {
        // Arrange
        var firstBoardColumnId = await SeedBoardColumnAsync("First board column");
        var secondBoardResponse = await Client.PostAsJsonAsync(
            "/api/boards",
            new CreateBoardRequest("Second board"));
        secondBoardResponse.EnsureSuccessStatusCode();
        var secondBoardEnvelope = await secondBoardResponse.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>(JsonOptions);
        var secondBoard = Assert.IsType<BoardDto>(secondBoardEnvelope!.Data);
        var secondBoardColumnId = secondBoard.Columns[0].Id;

        var firstCreateResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(firstBoardColumnId, "First board card", "", []));
        var secondCreateResponse = await Client.PostAsJsonAsync(
            $"/api/boards/{secondBoard.Id}/cards",
            new CreateCardRequest(secondBoardColumnId, "Second board card", "", []));
        firstCreateResponse.EnsureSuccessStatusCode();
        secondCreateResponse.EnsureSuccessStatusCode();
        var firstCard = Assert.IsType<CardDto>(
            (await firstCreateResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions))!.Data);
        var secondCard = Assert.IsType<CardDto>(
            (await secondCreateResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions))!.Data);
        Assert.Equal(firstCard.Id, secondCard.Id);

        // Act
        var deleteResponse = await Client.DeleteAsync($"/api/boards/1/cards/{firstCard.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        var secondBoardReadResponse = await Client.GetAsync($"/api/boards/{secondBoard.Id}");
        secondBoardReadResponse.EnsureSuccessStatusCode();
        var secondBoardRead = Assert.IsType<BoardDto>(
            (await secondBoardReadResponse.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>(JsonOptions))!.Data);
        var remainingCard = Assert.Single(secondBoardRead.Columns.SelectMany(x => x.Cards));
        Assert.Equal(secondCard.Id, remainingCard.Id);
        Assert.Equal("Second board card", remainingCard.Title);
    }

    [Fact]
    public async Task CardEndpoints_Search_ShouldBindFilterArrayAndReturnMatchingCards()
    {
        // Arrange
        var createdColumnId = await SeedBoardColumnAsync("Todo");
        var firstCreateResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(
                createdColumnId,
                "First match",
                "Desc",
                [],
                ExternalUrl: "https://github.com/Example/repository"));
        firstCreateResponse.EnsureSuccessStatusCode();
        var secondCreateResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(
                createdColumnId,
                "Second match",
                "Desc",
                [],
                ExternalUrl: "https://github.com/example/repository/issues"));
        secondCreateResponse.EnsureSuccessStatusCode();

        // Act
        var response = await Client.PostAsJsonAsync(
            "/api/boards/1/cards/search",
            new SearchCardsRequest([
                new CardSearchFilterRequest(
                    CardSearchFields.ExternalUrl,
                    CardSearchOperators.Contains,
                    "GITHUB.COM/example/repository")
            ]));
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<IReadOnlyList<CardDto>>>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
        Assert.NotNull(envelope.Data);
        Assert.Equal(["Second match", "First match"], envelope.Data!.Select(x => x.Title).ToArray());
        Assert.All(envelope.Data, card => Assert.NotNull(card.ExternalUrl));
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
    public async Task CardEndpoints_Edit_WithBulkMove_ShouldReturnSuccessContract()
    {
        // Arrange
        var createdTodoColumnId = await SeedBoardColumnAsync("Todo");
        var createdDoingColumnId = await SeedBoardColumnAsync("Doing");
        var firstCardId = await SeedBoardCardAsync(createdTodoColumnId, "Task A", "Desc");
        var secondCardId = await SeedBoardCardAsync(createdTodoColumnId, "Task B", "Desc");

        // Act
        var response = await Client.PatchAsJsonAsync(
            "/api/boards/1/cards/edit",
            new BulkEditCardsRequest(
                [secondCardId, firstCardId],
                new BulkMoveCardsRequest(createdDoingColumnId, null)));
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<IReadOnlyList<CardDto>>>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
        Assert.NotNull(envelope.Data);
        Assert.Equal([firstCardId, secondCardId], envelope.Data!.Select(x => x.Id).ToArray());
        Assert.All(envelope.Data, card => Assert.Equal(createdDoingColumnId, card.BoardColumnId));
    }

    [Fact]
    public async Task CardEndpoints_Edit_WithBulkSlickChange_ShouldReturnUpdatedSlickMembershipContract()
    {
        // Arrange
        var createdTodoColumnId = await SeedBoardColumnAsync("Todo");
        var firstCardId = await SeedBoardCardAsync(createdTodoColumnId, "Task A", "Desc");
        var secondCardId = await SeedBoardCardAsync(createdTodoColumnId, "Task B", "Desc");

        // Act
        var setResponse = await Client.PatchAsJsonAsync(
            "/api/boards/1/cards/edit",
            new BulkEditCardsRequest(
                [firstCardId, secondCardId],
                Move: null,
                Slick: new BulkEditSlickRequest("Release train")));
        var setEnvelope = await setResponse.Content.ReadFromJsonAsync<ApiEnvelope<IReadOnlyList<CardDto>>>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, setResponse.StatusCode);
        Assert.NotNull(setEnvelope);
        Assert.True(setEnvelope!.Success);
        Assert.NotNull(setEnvelope.Data);
        Assert.All(setEnvelope.Data!, card =>
        {
            Assert.NotNull(card.SlickId);
            Assert.Equal("Release train", card.SlickName);
        });

        // Act
        var clearResponse = await Client.PatchAsJsonAsync(
            "/api/boards/1/cards/edit",
            new BulkEditCardsRequest(
                [firstCardId, secondCardId],
                Move: null,
                Slick: new BulkEditSlickRequest(null)));
        var clearEnvelope = await clearResponse.Content.ReadFromJsonAsync<ApiEnvelope<IReadOnlyList<CardDto>>>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
        Assert.NotNull(clearEnvelope);
        Assert.True(clearEnvelope!.Success);
        Assert.NotNull(clearEnvelope.Data);
        Assert.All(clearEnvelope.Data!, card =>
        {
            Assert.Null(card.SlickId);
            Assert.Null(card.SlickName);
        });
    }

    [Fact]
    public async Task CardEndpoints_Edit_WithOverlongBulkSlickName_ShouldReturnValidationError()
    {
        // Arrange
        var createdTodoColumnId = await SeedBoardColumnAsync("Todo");
        var firstCardId = await SeedBoardCardAsync(createdTodoColumnId, "Task A", "Desc");
        var overlongSlickName = new string('X', 41);

        // Act
        var response = await Client.PatchAsJsonAsync(
            "/api/boards/1/cards/edit",
            new BulkEditCardsRequest(
                [firstCardId],
                Move: null,
                Slick: new BulkEditSlickRequest(overlongSlickName)));
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<IReadOnlyList<CardDto>>>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.False(envelope!.Success);
        Assert.Equal(400, envelope.StatusCode);
        Assert.NotNull(envelope.ValidationErrors);
        Assert.Contains("slick.name", envelope.ValidationErrors!.Keys);
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
    public async Task CardEndpoints_GetArchivedById_ShouldIncludeSlickMembershipInCardContract()
    {
        // Arrange
        var slickCreateResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/slicks",
            new CreateSlickRequest("Release train", "presets", """{"presetIndex":2}"""));
        slickCreateResponse.EnsureSuccessStatusCode();
        var slickEnvelope = await slickCreateResponse.Content.ReadFromJsonAsync<ApiEnvelope<SlickDto>>(JsonOptions);
        Assert.NotNull(slickEnvelope);
        Assert.NotNull(slickEnvelope!.Data);
        var slickId = slickEnvelope.Data!.Id;

        var createdColumnId = await SeedBoardColumnAsync("Todo");
        var createCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(createdColumnId, "Archive me", "Desc", [], null, null, slickEnvelope.Data.Name));
        createCardResponse.EnsureSuccessStatusCode();
        var createdCardEnvelope = await createCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);
        Assert.NotNull(createdCardEnvelope);
        Assert.NotNull(createdCardEnvelope!.Data);

        var archiveResponse = await Client.PostAsync($"/api/boards/1/cards/{createdCardEnvelope.Data!.Id}/archive", content: null);
        archiveResponse.EnsureSuccessStatusCode();
        var archivedCardEnvelope = await archiveResponse.Content.ReadFromJsonAsync<ApiEnvelope<ArchivedCardDto>>(JsonOptions);
        Assert.NotNull(archivedCardEnvelope);
        Assert.NotNull(archivedCardEnvelope!.Data);
        var archivedCardId = archivedCardEnvelope.Data!.Id;

        // Act
        var response = await Client.GetAsync($"/api/boards/1/cards/archived/{archivedCardId}");
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<ArchivedCardDetailDto>>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.Success);
        Assert.NotNull(payload.Data);
        Assert.Equal(slickId, payload.Data!.Card.SlickId);
        Assert.Equal("Release train", payload.Data.Card.SlickName);
    }

    [Fact]
    public async Task CardEndpoints_Unarchive_ShouldReturnRestoredCardContract()
    {
        // Arrange
        var createdColumnId = await SeedBoardColumnAsync("Todo");
        var slickCreateResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/slicks",
            new CreateSlickRequest("Release train", "presets", """{"presetIndex":2}"""));
        slickCreateResponse.EnsureSuccessStatusCode();
        var slickEnvelope = await slickCreateResponse.Content.ReadFromJsonAsync<ApiEnvelope<SlickDto>>(JsonOptions);
        Assert.NotNull(slickEnvelope);
        Assert.NotNull(slickEnvelope!.Data);
        var slickId = slickEnvelope.Data!.Id;

        var createCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(createdColumnId, "Archive me", "Desc", [], null, null, slickEnvelope.Data.Name));
        createCardResponse.EnsureSuccessStatusCode();
        var createdCardEnvelope = await createCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);
        Assert.NotNull(createdCardEnvelope);
        Assert.NotNull(createdCardEnvelope!.Data);
        var createdCardId = createdCardEnvelope.Data!.Id;

        var archiveResponse = await Client.PostAsync($"/api/boards/1/cards/{createdCardId}/archive", content: null);
        archiveResponse.EnsureSuccessStatusCode();
        var archivedCardEnvelope = await archiveResponse.Content.ReadFromJsonAsync<ApiEnvelope<ArchivedCardDto>>(JsonOptions);
        Assert.NotNull(archivedCardEnvelope);
        Assert.NotNull(archivedCardEnvelope!.Data);
        var archivedCardId = archivedCardEnvelope.Data!.Id;

        // Act
        var response = await Client.PostAsync($"/api/boards/1/cards/archived/{archivedCardId}/unarchive", content: null);
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.Success);
        Assert.NotNull(payload.Data);
        Assert.Equal(createdCardId, payload.Data!.Id);
        Assert.Equal("Archive me", payload.Data.Title);
        Assert.Equal(createdColumnId, payload.Data.BoardColumnId);
        Assert.Equal(slickId, payload.Data.SlickId);
        Assert.Equal("Release train", payload.Data.SlickName);
    }

    [Fact]
    public async Task CardEndpoints_Unarchive_WithExhaustedLeadingKeys_ShouldReturnRestoredCardContract()
    {
        // Arrange
        var columnId = await SeedBoardColumnAsync("Todo");
        var archivedCardId = await SeedBoardCardAsync(columnId, "Restore me", "Desc");
        var archiveResponse = await Client.PostAsync($"/api/boards/1/cards/{archivedCardId}/archive", content: null);
        archiveResponse.EnsureSuccessStatusCode();
        var archivedCardEnvelope = await archiveResponse.Content.ReadFromJsonAsync<ApiEnvelope<ArchivedCardDto>>(JsonOptions);
        Assert.NotNull(archivedCardEnvelope);
        Assert.NotNull(archivedCardEnvelope!.Data);
        var targetAId = await SeedBoardCardAsync(columnId, "Target A", "Desc");
        var targetBId = await SeedBoardCardAsync(columnId, "Target B", "Desc");
        await ArrangeAsync(async dbContext =>
        {
            var targetA = await dbContext.Cards.SingleAsync(card => card.Id == targetAId);
            var targetB = await dbContext.Cards.SingleAsync(card => card.Id == targetBId);
            targetA.SortKey = "00000000000000000000";
            targetB.SortKey = "00000000000000000001";
        });

        // Act
        var response = await Client.PostAsync(
            $"/api/boards/1/cards/archived/{archivedCardEnvelope.Data!.Id}/unarchive",
            content: null);
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.Success);
        Assert.NotNull(payload.Data);
        Assert.Equal("Restore me", payload.Data!.Title);
        Assert.Equal(columnId, payload.Data.BoardColumnId);
        var orderedTitles = await UseDbContextAsync(dbContext => dbContext.Cards
            .Where(card => card.BoardColumnId == columnId)
            .OrderBy(card => card.SortKey)
            .Select(card => card.Title)
            .ToListAsync());
        Assert.Equal(["Restore me", "Target A", "Target B"], orderedTitles);
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

    [Fact]
    public async Task CardEndpoints_DeleteBulk_ShouldReturnSummaryContract()
    {
        // Arrange
        var createdColumnId = await SeedBoardColumnAsync("Todo");
        var firstCardId = await SeedBoardCardAsync(createdColumnId, "Delete A", "Desc");
        var secondCardId = await SeedBoardCardAsync(createdColumnId, "Delete B", "Desc");

        // Act
        var response = await Client.PostAsJsonAsync(
            "/api/boards/1/cards/delete",
            new BulkDeleteCardsRequest([firstCardId, secondCardId]));
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<BulkDeleteCardsSummaryDto>>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
        Assert.NotNull(envelope.Data);
        Assert.Equal(1, envelope.Data!.BoardId);
        Assert.Equal(2, envelope.Data.RequestedCount);
        Assert.Equal(2, envelope.Data.DeletedCount);
    }

    [Fact]
    public async Task CardCommentsEndpoints_ShouldCreateAndListComments()
    {
        // Arrange
        var columnId = await SeedBoardColumnAsync("Todo");
        var cardId = await SeedBoardCardAsync(columnId, "Task A", "Desc");

        // Act
        var createResponse = await Client.PostAsJsonAsync(
            $"/api/boards/1/cards/{cardId}/comments",
            new CreateCardCommentRequest("First comment"));
        var createdEnvelope = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardCommentDto>>(JsonOptions);
        var listResponse = await Client.GetAsync($"/api/boards/1/cards/{cardId}/comments");
        var listEnvelope = await listResponse.Content.ReadFromJsonAsync<ApiEnvelope<IReadOnlyList<CardCommentDto>>>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createdEnvelope);
        Assert.True(createdEnvelope!.Success);
        Assert.NotNull(createdEnvelope.Data);
        Assert.Equal(cardId, createdEnvelope.Data!.CardId);
        Assert.Equal("First comment", createdEnvelope.Data!.Text);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(listEnvelope);
        Assert.True(listEnvelope!.Success);
        Assert.NotNull(listEnvelope.Data);
        Assert.Single(listEnvelope.Data);
        Assert.Equal(cardId, listEnvelope.Data[0].CardId);
        Assert.Equal("First comment", listEnvelope.Data[0].Text);
    }

}
