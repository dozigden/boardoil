using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Column;
using BoardOil.Contracts.Tag;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class BoardApiBoardAndColumnIntegrationTests
    : BoardApiIntegrationTestBase
{
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

}
