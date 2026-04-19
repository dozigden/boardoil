using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.CardType;
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
    public async Task ExportBoard_ShouldReturnZipWithManifestAndBoardPayload()
    {
        var createTagResponse = await Client.PostAsJsonAsync("/api/boards/1/tags", new CreateTagRequest("ExportTag"));
        createTagResponse.EnsureSuccessStatusCode();
        var createTagEnvelope = await createTagResponse.Content.ReadFromJsonAsync<ApiEnvelope<TagDto>>(JsonOptions);
        Assert.NotNull(createTagEnvelope);
        Assert.NotNull(createTagEnvelope!.Data);
        var tagId = createTagEnvelope.Data!.Id;

        var updateTagResponse = await Client.PutAsJsonAsync(
            $"/api/boards/1/tags/{tagId}",
            new UpdateTagStyleRequest("ExportTag", "solid", """{"backgroundColor":"#224466","textColorMode":"auto"}""", "🧪"));
        updateTagResponse.EnsureSuccessStatusCode();

        var boardBeforeExport = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/boards/1", JsonOptions);
        Assert.NotNull(boardBeforeExport);
        Assert.NotNull(boardBeforeExport!.Data);
        var todoColumnId = boardBeforeExport.Data!.Columns[0].Id;

        var createCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(todoColumnId, "Export card", "Export description", ["ExportTag"]));
        createCardResponse.EnsureSuccessStatusCode();

        var exportResponse = await Client.GetAsync("/api/boards/1/export");

        Assert.Equal(HttpStatusCode.OK, exportResponse.StatusCode);
        Assert.Equal("application/zip", exportResponse.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(exportResponse.Content.Headers.ContentDisposition);
        Assert.NotNull(exportResponse.Content.Headers.ContentDisposition!.FileName);
        Assert.Contains(".boardoil.zip", exportResponse.Content.Headers.ContentDisposition.FileName!, StringComparison.OrdinalIgnoreCase);

        var payloadBytes = await exportResponse.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(payloadBytes);

        using var payloadStream = new MemoryStream(payloadBytes);
        using var archive = new ZipArchive(payloadStream, ZipArchiveMode.Read);

        var manifestEntry = archive.GetEntry("manifest.json");
        Assert.NotNull(manifestEntry);
        using var manifestReader = new StreamReader(manifestEntry!.Open());
        var manifestJson = await manifestReader.ReadToEndAsync();
        var manifest = JsonSerializer.Deserialize<BoardPackageManifestDto>(manifestJson, JsonOptions);
        Assert.NotNull(manifest);
        Assert.Equal("boardoil-board-package", manifest!.Format);
        Assert.Equal(2, manifest.SchemaVersion);
        Assert.False(string.IsNullOrWhiteSpace(manifest.ExportedByVersion));
        Assert.Contains(manifest.Entries, x => x.Kind == "board" && x.Path == "board.json");

        var boardEntry = archive.GetEntry("board.json");
        Assert.NotNull(boardEntry);
        using var boardReader = new StreamReader(boardEntry!.Open());
        var boardJson = await boardReader.ReadToEndAsync();
        var boardPayload = JsonSerializer.Deserialize<BoardPackageBoardDto>(boardJson, JsonOptions);
        Assert.NotNull(boardPayload);
        Assert.Equal("BoardOil", boardPayload!.Name);
        Assert.Equal(string.Empty, boardPayload.Description);
        Assert.Contains(boardPayload.Columns, x => x.Title == "Todo");
        Assert.Contains(boardPayload.Columns.SelectMany(x => x.Cards), x => x.Title == "Export card" && x.CardTypeName == "Story");
        Assert.Contains(boardPayload.Tags, x => x.Name == "ExportTag" && x.StyleName == "solid" && x.Emoji == "🧪");
    }

    [Fact]
    public async Task ExportBoard_WhenUnauthenticated_ShouldReturnUnauthorized()
    {
        var unauthenticatedClient = Factory.CreateClient();

        var response = await unauthenticatedClient.GetAsync("/api/boards/1/export");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BootstrappedBoard_ShouldHaveSystemCardType()
    {
        await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(1)
            FROM "CardTypes"
            WHERE "BoardId" = 1
              AND "IsSystem" = 1
              AND "Name" = 'Story';
            """;

        var systemTypeCount = (long)(await command.ExecuteScalarAsync() ?? 0L);
        Assert.Equal(1, systemTypeCount);
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
        Assert.Equal(string.Empty, first.Data.Description);
        Assert.Equal(string.Empty, second.Data.Description);
        Assert.Equal(["Todo", "In Progress", "Done"], first.Data.Columns.Select(x => x.Title).ToArray());
        Assert.Equal(["Todo", "In Progress", "Done"], second.Data.Columns.Select(x => x.Title).ToArray());
    }

    [Fact]
    public async Task CreateBoard_ShouldCreateSystemCardType()
    {
        // Arrange
        var createBoardResponse = await Client.PostAsJsonAsync("/api/boards", new CreateBoardRequest("CardType Seed"));
        createBoardResponse.EnsureSuccessStatusCode();
        var createdBoard = await createBoardResponse.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>(JsonOptions);
        Assert.NotNull(createdBoard);
        Assert.NotNull(createdBoard!.Data);
        var boardId = createdBoard.Data!.Id;

        // Act
        await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(1)
            FROM "CardTypes"
            WHERE "BoardId" = $boardId
              AND "IsSystem" = 1
              AND "Name" = 'Story';
            """;
        command.Parameters.AddWithValue("$boardId", boardId);
        var systemTypeCount = (long)(await command.ExecuteScalarAsync() ?? 0L);

        // Assert
        Assert.Equal(1, systemTypeCount);
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
        Assert.Equal(string.Empty, updated.Data.Description);

        var boardList = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<BoardSummaryDto>>>("/api/boards", JsonOptions);
        Assert.NotNull(boardList);
        Assert.NotNull(boardList!.Data);
        Assert.Contains(boardList.Data!, x => x.Id == created.Data.Id && x.Name == "Product Roadmap" && x.Description == string.Empty);
    }

    [Fact]
    public async Task BoardEndpoints_ShouldRoundTripDescriptionOnCreateAndUpdate()
    {
        // Arrange
        var createResponse = await Client.PostAsJsonAsync(
            "/api/boards",
            new CreateBoardRequest("Roadmap", "  Initial board guidance  "));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>(JsonOptions);
        Assert.NotNull(created);
        Assert.NotNull(created!.Data);
        Assert.Equal("Initial board guidance", created.Data!.Description);

        // Act
        var updateResponse = await Client.PutAsJsonAsync(
            $"/api/boards/{created.Data.Id}",
            new UpdateBoardRequest("Roadmap", "  Updated board guidance  "));
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<ApiEnvelope<BoardSummaryDto>>(JsonOptions);
        Assert.NotNull(updated);
        Assert.NotNull(updated!.Data);
        Assert.Equal("Updated board guidance", updated.Data!.Description);

        // Assert
        var board = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>($"/api/boards/{created.Data.Id}", JsonOptions);
        Assert.NotNull(board);
        Assert.NotNull(board!.Data);
        Assert.Equal("Updated board guidance", board.Data!.Description);
    }

    [Fact]
    public async Task BoardEndpoints_WhenDescriptionExceedsLimit_ShouldReturnBadRequest()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/boards",
            new CreateBoardRequest("Roadmap", new string('D', 5_001)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal("Validation failed.", payload.Message);
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
    public async Task Swagger_RequestSchemas_ShouldMarkNonNullableReferencePropertiesAsRequired()
    {
        // Arrange
        var swaggerResponse = await Client.GetAsync("/swagger/v1/swagger.json");
        swaggerResponse.EnsureSuccessStatusCode();
        await using var swaggerStream = await swaggerResponse.Content.ReadAsStreamAsync();
        using var swaggerDocument = await JsonDocument.ParseAsync(swaggerStream);

        var components = swaggerDocument.RootElement.GetProperty("components");
        var schemas = components.GetProperty("schemas");
        var createCardSchema = schemas.GetProperty("CreateCardRequest");
        var required = createCardSchema.TryGetProperty("required", out var requiredElement)
            ? requiredElement.EnumerateArray().Select(x => x.GetString()).ToArray()
            : [];
        var properties = createCardSchema.GetProperty("properties");
        var boardColumnIdSchema = properties.GetProperty("boardColumnId");
        var titleSchema = properties.GetProperty("title");
        var descriptionSchema = properties.GetProperty("description");

        // Assert: CreateCardRequest
        Assert.DoesNotContain("boardColumnId", required);
        Assert.Contains("title", required);
        Assert.DoesNotContain("description", required);
        Assert.True(boardColumnIdSchema.TryGetProperty("nullable", out var boardColumnNullable));
        Assert.True(boardColumnNullable.GetBoolean());
        Assert.False(titleSchema.TryGetProperty("nullable", out var titleNullable) && titleNullable.GetBoolean());
        Assert.True(descriptionSchema.TryGetProperty("nullable", out var descriptionNullable));
        Assert.True(descriptionNullable.GetBoolean());

        // Assert: CreateBoardRequest (proves this is global, not card-specific)
        var createBoardSchema = schemas.GetProperty("CreateBoardRequest");
        var createBoardRequired = createBoardSchema.GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray();
        var createBoardNameSchema = createBoardSchema.GetProperty("properties").GetProperty("name");
        Assert.Contains("name", createBoardRequired);
        Assert.False(createBoardNameSchema.TryGetProperty("nullable", out var createBoardNameNullable) && createBoardNameNullable.GetBoolean());
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
        Assert.Equal(["Bug", "Urgent"], createdCard.Data.Tags.Select(x => x.Name).ToArray());
        Assert.Equal(["Bug", "Urgent"], createdCard.Data.TagNames);
        Assert.True(createdCard.Data.CardTypeId > 0);
        Assert.Equal("Story", createdCard.Data.CardTypeName);
        Assert.Null(createdCard.Data.CardTypeEmoji);
    }

    [Trait("Category", "Integration")]
    [Trait("Consumer", "HA")]
    [Trait("Surface", "REST")]
    [Fact]
    public async Task CardEndpoints_CreateWithoutBoardColumnId_ShouldCreateCardInLeftMostColumn()
    {
        // Arrange
        var boardBefore = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/boards/1", JsonOptions);
        Assert.NotNull(boardBefore);
        Assert.NotNull(boardBefore!.Data);
        var leftMostColumnId = boardBefore.Data!.Columns[0].Id;

        // Act
        var createdCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new
            {
                title = "Task A",
                description = "Desc",
                tagNames = Array.Empty<string>()
            });
        createdCardResponse.EnsureSuccessStatusCode();
        var createdCard = await createdCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);

        // Assert
        Assert.NotNull(createdCard);
        Assert.NotNull(createdCard!.Data);
        Assert.Equal(leftMostColumnId, createdCard.Data!.BoardColumnId);
    }

    [Fact]
    public async Task CardEndpoints_CreateWithoutDescription_ShouldPersistEmptyDescription()
    {
        // Arrange
        var boardBefore = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/boards/1", JsonOptions);
        Assert.NotNull(boardBefore);
        Assert.NotNull(boardBefore!.Data);
        var leftMostColumnId = boardBefore.Data!.Columns[0].Id;

        // Act
        var createdCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new
            {
                boardColumnId = leftMostColumnId,
                title = "Task A"
            });
        createdCardResponse.EnsureSuccessStatusCode();
        var createdCard = await createdCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);

        // Assert
        Assert.NotNull(createdCard);
        Assert.NotNull(createdCard!.Data);
        Assert.Equal(string.Empty, createdCard.Data!.Description);
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

        var cardTypesEnvelope = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<CardTypeDto>>>("/api/boards/1/card-types", JsonOptions);
        Assert.NotNull(cardTypesEnvelope);
        Assert.NotNull(cardTypesEnvelope!.Data);
        var systemCardType = Assert.Single(cardTypesEnvelope.Data!, x => x.IsSystem);

        // Act
        var updatedCardResponse = await Client.PutAsJsonAsync(
            $"/api/boards/1/cards/{createdCard.Data!.Id}",
            new UpdateCardRequest("Task B", "Desc", ["Urgent"], systemCardType.Id));
        updatedCardResponse.EnsureSuccessStatusCode();

        // Assert
        var board = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/boards/1", JsonOptions);
        Assert.NotNull(board);
        Assert.NotNull(board!.Data);
        var createdColumnState = board.Data.Columns.FirstOrDefault(x => x.Id == createdColumn.Data.Id);
        Assert.NotNull(createdColumnState);
        Assert.Single(createdColumnState!.Cards);
        Assert.Equal("Task B", createdColumnState.Cards[0].Title);
        Assert.Equal(["Urgent"], createdColumnState.Cards[0].Tags.Select(x => x.Name).ToArray());
        Assert.Equal(["Urgent"], createdColumnState.Cards[0].TagNames);
    }

    [Fact]
    public async Task CardEndpoints_UpdateWithBoardColumnId_ShouldMoveCardToTopOfTargetColumn()
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

        var movingCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(createdTodoColumn.Data!.Id, "Move me", "Desc", null));
        movingCardResponse.EnsureSuccessStatusCode();
        var movingCard = await movingCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);
        Assert.NotNull(movingCard);
        Assert.NotNull(movingCard!.Data);

        var existingAResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(createdDoingColumn.Data!.Id, "Existing A", "Desc", null));
        existingAResponse.EnsureSuccessStatusCode();
        var existingBResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(createdDoingColumn.Data!.Id, "Existing B", "Desc", null));
        existingBResponse.EnsureSuccessStatusCode();

        var cardTypesEnvelope = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<CardTypeDto>>>("/api/boards/1/card-types", JsonOptions);
        Assert.NotNull(cardTypesEnvelope);
        Assert.NotNull(cardTypesEnvelope!.Data);
        var systemCardType = Assert.Single(cardTypesEnvelope.Data!, x => x.IsSystem);

        // Act
        var updatedCardResponse = await Client.PutAsJsonAsync(
            $"/api/boards/1/cards/{movingCard.Data!.Id}",
            new UpdateCardRequest("Move me updated", "Updated", [], systemCardType.Id, createdDoingColumn.Data.Id));
        updatedCardResponse.EnsureSuccessStatusCode();

        // Assert
        var board = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/boards/1", JsonOptions);
        Assert.NotNull(board);
        Assert.NotNull(board!.Data);
        var todoState = board.Data.Columns.Single(x => x.Id == createdTodoColumn.Data.Id);
        var doingState = board.Data.Columns.Single(x => x.Id == createdDoingColumn.Data.Id);
        Assert.Empty(todoState.Cards);
        Assert.Equal(["Move me updated", "Existing B", "Existing A"], doingState.Cards.Select(x => x.Title).ToArray());
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
    public async Task CardEndpoints_ShouldCreateAndUpdateCard_WithExplicitCardTypeId()
    {
        // Arrange
        var createdColumnResponse = await Client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Todo"));
        createdColumnResponse.EnsureSuccessStatusCode();
        var createdColumn = await createdColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(createdColumn);
        Assert.NotNull(createdColumn!.Data);

        var createdTypeResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/card-types",
            new CreateCardTypeRequest("Bug", "🐞"));
        createdTypeResponse.EnsureSuccessStatusCode();
        var createdTypeEnvelope = await createdTypeResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardTypeDto>>(JsonOptions);
        Assert.NotNull(createdTypeEnvelope);
        Assert.NotNull(createdTypeEnvelope!.Data);

        // Act: create with explicit type
        var createCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(createdColumn.Data!.Id, "Task A", "Desc", ["Bug"], createdTypeEnvelope.Data!.Id));
        createCardResponse.EnsureSuccessStatusCode();
        var createdCardEnvelope = await createCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);

        // Assert: created card uses selected type
        Assert.NotNull(createdCardEnvelope);
        Assert.NotNull(createdCardEnvelope!.Data);
        Assert.Equal(createdTypeEnvelope.Data.Id, createdCardEnvelope.Data!.CardTypeId);
        Assert.Equal("Bug", createdCardEnvelope.Data.CardTypeName);
        Assert.Equal("🐞", createdCardEnvelope.Data.CardTypeEmoji);

        // Act: update card type by selecting system type
        var allTypesEnvelope = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<CardTypeDto>>>("/api/boards/1/card-types", JsonOptions);
        Assert.NotNull(allTypesEnvelope);
        Assert.NotNull(allTypesEnvelope!.Data);
        var systemType = Assert.Single(allTypesEnvelope.Data!, x => x.IsSystem);

        var updateCardResponse = await Client.PutAsJsonAsync(
            $"/api/boards/1/cards/{createdCardEnvelope.Data.Id}",
            new UpdateCardRequest("Task A", "Desc", ["Bug"], systemType.Id));
        updateCardResponse.EnsureSuccessStatusCode();
        var updatedCardEnvelope = await updateCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);

        // Assert: updated card uses system type
        Assert.NotNull(updatedCardEnvelope);
        Assert.NotNull(updatedCardEnvelope!.Data);
        Assert.Equal(systemType.Id, updatedCardEnvelope.Data!.CardTypeId);
        Assert.Equal(systemType.Name, updatedCardEnvelope.Data.CardTypeName);
        Assert.Equal(systemType.Emoji, updatedCardEnvelope.Data.CardTypeEmoji);
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
        Assert.Equal(["Move me", "Existing B", "Existing A"], doingState!.Cards.Select(x => x.Title).ToArray());
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
        Assert.Equal(["Bug"], archivedCardEnvelope.Data.TagNames);
        Assert.False(string.IsNullOrWhiteSpace(archivedCardEnvelope.Data.SnapshotJson));

        var boardAfterArchive = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/boards/1", JsonOptions);
        Assert.NotNull(boardAfterArchive);
        Assert.NotNull(boardAfterArchive!.Data);
        var columnAfterArchive = boardAfterArchive.Data!.Columns.FirstOrDefault(x => x.Id == createdColumn.Data.Id);
        Assert.NotNull(columnAfterArchive);
        Assert.Empty(columnAfterArchive!.Cards);
    }

    [Fact]
    public async Task CardEndpoints_GetArchived_ShouldOrderNewestFirst_AndSearchByTitleAndTags()
    {
        // Arrange
        var createdColumnResponse = await Client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Todo"));
        createdColumnResponse.EnsureSuccessStatusCode();
        var createdColumn = await createdColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(createdColumn);
        Assert.NotNull(createdColumn!.Data);
        var createTagResponse = await Client.PostAsJsonAsync("/api/boards/1/tags", new CreateTagRequest("Urgent"));
        createTagResponse.EnsureSuccessStatusCode();
        var alphaCreateResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(createdColumn.Data!.Id, "Alpha item", "No marker", null));
        alphaCreateResponse.EnsureSuccessStatusCode();
        var alphaCard = await alphaCreateResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);
        Assert.NotNull(alphaCard);
        Assert.NotNull(alphaCard!.Data);
        var betaCreateResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(createdColumn.Data!.Id, "Beta item", "Contains needle marker", ["Urgent"]));
        betaCreateResponse.EnsureSuccessStatusCode();
        var betaCard = await betaCreateResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);
        Assert.NotNull(betaCard);
        Assert.NotNull(betaCard!.Data);

        var alphaArchiveResponse = await Client.PostAsync($"/api/boards/1/cards/{alphaCard.Data!.Id}/archive", content: null);
        alphaArchiveResponse.EnsureSuccessStatusCode();
        var betaArchiveResponse = await Client.PostAsync($"/api/boards/1/cards/{betaCard.Data!.Id}/archive", content: null);
        betaArchiveResponse.EnsureSuccessStatusCode();

        // Act
        var allArchivedResponse = await Client.GetFromJsonAsync<ApiEnvelope<ArchivedCardListDto>>("/api/boards/1/cards/archived", JsonOptions);
        var titleSearchResponse = await Client.GetFromJsonAsync<ApiEnvelope<ArchivedCardListDto>>("/api/boards/1/cards/archived?search=alpha", JsonOptions);
        var tagSearchResponse = await Client.GetFromJsonAsync<ApiEnvelope<ArchivedCardListDto>>("/api/boards/1/cards/archived?search=urgent", JsonOptions);
        var descriptionSearchResponse = await Client.GetFromJsonAsync<ApiEnvelope<ArchivedCardListDto>>("/api/boards/1/cards/archived?search=needle", JsonOptions);
        var pagedArchivedResponse = await Client.GetFromJsonAsync<ApiEnvelope<ArchivedCardListDto>>("/api/boards/1/cards/archived?offset=1&limit=1", JsonOptions);

        // Assert
        Assert.NotNull(allArchivedResponse);
        Assert.NotNull(allArchivedResponse!.Data);
        Assert.Equal(["Beta item", "Alpha item"], allArchivedResponse.Data!.Items.Select(x => x.Title).ToArray());
        Assert.Equal(2, allArchivedResponse.Data.TotalCount);

        Assert.NotNull(titleSearchResponse);
        Assert.NotNull(titleSearchResponse!.Data);
        var titleMatch = Assert.Single(titleSearchResponse.Data!.Items);
        Assert.Equal("Alpha item", titleMatch.Title);

        Assert.NotNull(tagSearchResponse);
        Assert.NotNull(tagSearchResponse!.Data);
        var tagMatch = Assert.Single(tagSearchResponse.Data!.Items);
        Assert.Equal("Beta item", tagMatch.Title);

        Assert.NotNull(descriptionSearchResponse);
        Assert.NotNull(descriptionSearchResponse!.Data);
        Assert.Empty(descriptionSearchResponse.Data!.Items);

        Assert.NotNull(pagedArchivedResponse);
        Assert.NotNull(pagedArchivedResponse!.Data);
        Assert.Equal(1, pagedArchivedResponse.Data!.Offset);
        Assert.Equal(1, pagedArchivedResponse.Data.Limit);
        Assert.Equal(2, pagedArchivedResponse.Data.TotalCount);
        var pagedCard = Assert.Single(pagedArchivedResponse.Data.Items);
        Assert.Equal("Alpha item", pagedCard.Title);
    }

    [Fact]
    public async Task CardEndpoints_GetArchived_WhenPaginationInvalid_ShouldReturnBadRequest()
    {
        // Act
        var response = await Client.GetAsync("/api/boards/1/cards/archived?offset=-1&limit=0");
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<ArchivedCardListDto>>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal(400, payload.StatusCode);
        Assert.Equal("Invalid pagination parameters.", payload.Message);
        Assert.NotNull(payload.ValidationErrors);
        Assert.Contains("offset", payload.ValidationErrors!.Keys, StringComparer.Ordinal);
        Assert.Contains("limit", payload.ValidationErrors.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public async Task CardEndpoints_GetArchivedById_ShouldReturnSnapshot()
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
        var archiveResponse = await Client.PostAsync($"/api/boards/1/cards/{createdCard.Data!.Id}/archive", content: null);
        archiveResponse.EnsureSuccessStatusCode();
        var archivedCardEnvelope = await archiveResponse.Content.ReadFromJsonAsync<ApiEnvelope<ArchivedCardDto>>(JsonOptions);
        Assert.NotNull(archivedCardEnvelope);
        Assert.NotNull(archivedCardEnvelope!.Data);

        // Act
        var archivedByIdResponse = await Client.GetFromJsonAsync<ApiEnvelope<ArchivedCardDto>>(
            $"/api/boards/1/cards/archived/{archivedCardEnvelope.Data!.Id}",
            JsonOptions);

        // Assert
        Assert.NotNull(archivedByIdResponse);
        Assert.NotNull(archivedByIdResponse!.Data);
        Assert.Equal("Archive me", archivedByIdResponse.Data!.Title);
        Assert.False(string.IsNullOrWhiteSpace(archivedByIdResponse.Data.SnapshotJson));
    }

    [Fact]
    public async Task CardEndpoints_GetArchivedById_WhenMissing_ShouldReturnNotFound()
    {
        // Act
        var response = await Client.GetAsync("/api/boards/1/cards/archived/999999");
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<ArchivedCardDto>>(JsonOptions);

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
        Assert.Equal(["Urgent"], cardState.Tags.Select(x => x.Name).ToArray());
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

    [Fact]
    public async Task CardTypeEndpoints_ShouldCreateListUpdateAndDelete_WithCardReassignment()
    {
        // Arrange
        var createColumnResponse = await Client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Todo"));
        createColumnResponse.EnsureSuccessStatusCode();
        var columnEnvelope = await createColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(columnEnvelope);
        Assert.NotNull(columnEnvelope!.Data);

        var createCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(columnEnvelope.Data!.Id, "Task A", "Desc", null));
        createCardResponse.EnsureSuccessStatusCode();
        var cardEnvelope = await createCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);
        Assert.NotNull(cardEnvelope);
        Assert.NotNull(cardEnvelope!.Data);

        // Act: create
        var createTypeResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/card-types",
            new CreateCardTypeRequest("Bug", "🐞"));
        createTypeResponse.EnsureSuccessStatusCode();
        var createdTypeEnvelope = await createTypeResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardTypeDto>>(JsonOptions);

        // Assert: create
        Assert.NotNull(createdTypeEnvelope);
        Assert.NotNull(createdTypeEnvelope!.Data);
        Assert.Equal(201, createdTypeEnvelope.StatusCode);
        Assert.False(createdTypeEnvelope.Data!.IsSystem);
        Assert.Equal("Bug", createdTypeEnvelope.Data.Name);
        Assert.Equal("🐞", createdTypeEnvelope.Data.Emoji);

        // Act: list
        var listEnvelope = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<CardTypeDto>>>("/api/boards/1/card-types", JsonOptions);

        // Assert: list
        Assert.NotNull(listEnvelope);
        Assert.NotNull(listEnvelope!.Data);
        Assert.Contains(listEnvelope.Data!, x => x.IsSystem && x.Name == "Story");
        Assert.Contains(listEnvelope.Data!, x => x.Name == "Bug");
        var systemType = Assert.Single(listEnvelope.Data!, x => x.IsSystem);
        var bugType = Assert.Single(listEnvelope.Data!, x => x.Name == "Bug");

        // Act: update
        var updateTypeResponse = await Client.PutAsJsonAsync(
            $"/api/boards/1/card-types/{bugType.Id}",
            new UpdateCardTypeRequest("Defect", "⚠️"));
        updateTypeResponse.EnsureSuccessStatusCode();
        var updatedTypeEnvelope = await updateTypeResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardTypeDto>>(JsonOptions);

        // Assert: update
        Assert.NotNull(updatedTypeEnvelope);
        Assert.NotNull(updatedTypeEnvelope!.Data);
        Assert.Equal("Defect", updatedTypeEnvelope.Data!.Name);
        Assert.Equal("⚠️", updatedTypeEnvelope.Data.Emoji);
        Assert.False(updatedTypeEnvelope.Data.IsSystem);

        await AssignCardTypeToCardAsync(cardEnvelope.Data!.Id, bugType.Id);

        // Act: delete non-system
        var deleteTypeResponse = await Client.DeleteAsync($"/api/boards/1/card-types/{bugType.Id}");
        deleteTypeResponse.EnsureSuccessStatusCode();

        // Assert: card reassigned
        var reassignedCardTypeId = await GetCardTypeIdForCardAsync(cardEnvelope.Data.Id);
        Assert.Equal(systemType.Id, reassignedCardTypeId);

        var listAfterDelete = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<CardTypeDto>>>("/api/boards/1/card-types", JsonOptions);
        Assert.NotNull(listAfterDelete);
        Assert.NotNull(listAfterDelete!.Data);
        Assert.DoesNotContain(listAfterDelete.Data!, x => x.Id == bugType.Id);
    }

    [Fact]
    public async Task CardTypeEndpoints_SetDefault_ShouldUseNewDefaultForCreatedCards()
    {
        // Arrange
        var createColumnResponse = await Client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Todo"));
        createColumnResponse.EnsureSuccessStatusCode();
        var columnEnvelope = await createColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(columnEnvelope);
        Assert.NotNull(columnEnvelope!.Data);

        var createTypeResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/card-types",
            new CreateCardTypeRequest("Bug", "🐞"));
        createTypeResponse.EnsureSuccessStatusCode();
        var createdTypeEnvelope = await createTypeResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardTypeDto>>(JsonOptions);
        Assert.NotNull(createdTypeEnvelope);
        Assert.NotNull(createdTypeEnvelope!.Data);

        // Act: switch default card type
        var setDefaultResponse = await Client.PatchAsync($"/api/boards/1/card-types/{createdTypeEnvelope.Data!.Id}/default", null);
        setDefaultResponse.EnsureSuccessStatusCode();

        // Assert: card type flags updated
        var listEnvelope = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<CardTypeDto>>>("/api/boards/1/card-types", JsonOptions);
        Assert.NotNull(listEnvelope);
        Assert.NotNull(listEnvelope!.Data);
        var bugType = Assert.Single(listEnvelope.Data!, x => x.Name == "Bug");
        var storyType = Assert.Single(listEnvelope.Data!, x => x.Name == "Story");
        Assert.True(bugType.IsSystem);
        Assert.False(storyType.IsSystem);

        // Assert: create-card default follows switched card type
        var createCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(columnEnvelope.Data.Id, "Task with default", "Desc", null));
        createCardResponse.EnsureSuccessStatusCode();
        var createdCardEnvelope = await createCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);
        Assert.NotNull(createdCardEnvelope);
        Assert.NotNull(createdCardEnvelope!.Data);
        Assert.Equal(bugType.Id, createdCardEnvelope.Data!.CardTypeId);
        Assert.Equal("Bug", createdCardEnvelope.Data.CardTypeName);
        Assert.Equal("🐞", createdCardEnvelope.Data.CardTypeEmoji);
    }

    [Fact]
    public async Task CardTypeEndpoints_WhenDeletingSystemType_ShouldReturnBadRequest()
    {
        // Arrange
        var listEnvelope = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<CardTypeDto>>>("/api/boards/1/card-types", JsonOptions);
        Assert.NotNull(listEnvelope);
        Assert.NotNull(listEnvelope!.Data);
        var systemType = Assert.Single(listEnvelope.Data!, x => x.IsSystem);

        // Act
        var response = await Client.DeleteAsync($"/api/boards/1/card-types/{systemType.Id}");
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions);

        // Assert
        Assert.Equal(400, (int)response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal(400, payload.StatusCode);
        Assert.Equal("System card type cannot be deleted.", payload.Message);
    }

    [Fact]
    public async Task CardTypeEndpoints_WhenDuplicateNameSubmitted_ShouldReturnBadRequest()
    {
        // Arrange
        var firstCreate = await Client.PostAsJsonAsync("/api/boards/1/card-types", new CreateCardTypeRequest("Feature"));
        firstCreate.EnsureSuccessStatusCode();

        // Act
        var response = await Client.PostAsJsonAsync("/api/boards/1/card-types", new CreateCardTypeRequest("  feature  "));
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions);

        // Assert
        Assert.Equal(400, (int)response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal(400, payload.StatusCode);
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

    private async Task AssignCardTypeToCardAsync(int cardId, int cardTypeId)
    {
        await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE "Cards"
            SET "CardTypeId" = $cardTypeId
            WHERE "Id" = $cardId;
            """;
        command.Parameters.AddWithValue("$cardId", cardId);
        command.Parameters.AddWithValue("$cardTypeId", cardTypeId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<int> GetCardTypeIdForCardAsync(int cardId)
    {
        await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT "CardTypeId"
            FROM "Cards"
            WHERE "Id" = $cardId;
            """;
        command.Parameters.AddWithValue("$cardId", cardId);
        var value = await command.ExecuteScalarAsync();
        return Convert.ToInt32(value);
    }

    private sealed record ApiEnvelope<T>(
        bool Success,
        T? Data,
        int StatusCode,
        string? Message,
        Dictionary<string, string[]>? ValidationErrors = null);
}
