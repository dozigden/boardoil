using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Column;
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
        Assert.True(result.Data.SlickCohesionModeEnabled);
        Assert.Equal(3, result.Data.Columns.Count);
        Assert.Equal("Todo", result.Data.Columns[0].Title);
        Assert.Equal("In Progress", result.Data.Columns[1].Title);
        Assert.Equal("Done", result.Data.Columns[2].Title);
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
    public async Task BoardEndpoints_CreateAndUpdate_ShouldReturnSuccessContracts()
    {
        // Arrange
        var createResponse = await Client.PostAsJsonAsync(
            "/api/boards",
            new CreateBoardRequest("Roadmap", "  Initial board guidance  "));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>(JsonOptions);
        Assert.NotNull(created);
        Assert.NotNull(created!.Data);
        Assert.Equal(201, (int)createResponse.StatusCode);
        Assert.True(created.Success);
        Assert.True(created.Data.SlickCohesionModeEnabled);

        // Act
        var updateResponse = await Client.PutAsJsonAsync(
            $"/api/boards/{created.Data.Id}",
            new UpdateBoardRequest("Roadmap", false, "  Updated board guidance  "));
        var updated = await updateResponse.Content.ReadFromJsonAsync<ApiEnvelope<BoardSummaryDto>>(JsonOptions);
        Assert.NotNull(updated);

        // Assert
        Assert.Equal(200, (int)updateResponse.StatusCode);
        Assert.True(updated!.Success);
        Assert.NotNull(updated.Data);
        Assert.Equal(created.Data.Id, updated.Data!.Id);
        Assert.False(updated.Data.SlickCohesionModeEnabled);
    }

    [Fact]
    public async Task UpdateBoard_WhenSlickCohesionModeFieldIsMissing_ShouldDefaultToFalse()
    {
        // Arrange
        var payload = """{"name":"Roadmap","description":"Updated board guidance"}""";

        // Act
        var response = await Client.PutAsync(
            "/api/boards/1",
            new StringContent(payload, Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadFromJsonAsync<ApiEnvelope<BoardSummaryDto>>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.Equal(200, body.StatusCode);
        Assert.NotNull(body.Data);
        Assert.False(body.Data!.SlickCohesionModeEnabled);
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
    public async Task ColumnEndpoints_CreateAndDelete_ShouldReturnSuccessContracts()
    {
        // Arrange
        var createdColumnResponse = await Client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Todo"));
        var createdColumn = await createdColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(createdColumn);
        Assert.NotNull(createdColumn!.Data);
        Assert.Equal(201, (int)createdColumnResponse.StatusCode);
        Assert.True(createdColumn.Success);

        // Act
        var deleteColumnResponse = await Client.DeleteAsync($"/api/boards/1/columns/{createdColumn.Data.Id}");
        var deleted = await deleteColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions);

        // Assert deleted
        Assert.Equal(200, (int)deleteColumnResponse.StatusCode);
        Assert.NotNull(deleted);
        Assert.True(deleted!.Success);
    }

    [Fact]
    public async Task ColumnEndpoints_Move_ShouldReturnSuccessContract()
    {
        // Arrange
        var createdSecondColumnResponse = await Client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("B"));
        var createdSecondColumn = await createdSecondColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(createdSecondColumn);
        Assert.NotNull(createdSecondColumn!.Data);

        // Act
        var moveResponse = await Client.PatchAsJsonAsync(
            $"/api/boards/1/columns/{createdSecondColumn.Data!.Id}/move",
            new MoveColumnRequest(null));
        var moved = await moveResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);

        // Assert
        Assert.Equal(200, (int)moveResponse.StatusCode);
        Assert.NotNull(moved);
        Assert.True(moved!.Success);
        Assert.NotNull(moved.Data);
        Assert.Equal(createdSecondColumn.Data.Id, moved.Data!.Id);
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
    public async Task Swagger_CardSearchOperation_ShouldDescribeBehaviourResponsesAndReadScope()
    {
        // Arrange
        var swaggerResponse = await Client.GetAsync("/swagger/v1/swagger.json");
        swaggerResponse.EnsureSuccessStatusCode();
        await using var swaggerStream = await swaggerResponse.Content.ReadAsStreamAsync();
        using var swaggerDocument = await JsonDocument.ParseAsync(swaggerStream);

        // Act
        var operation = swaggerDocument.RootElement
            .GetProperty("paths")
            .GetProperty("/api/boards/{boardId}/cards/search")
            .GetProperty("post");
        var responses = operation.GetProperty("responses");
        var successContent = responses
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");
        var patScopes = operation
            .GetProperty("x-pat-scopes")
            .EnumerateArray()
            .Select(x => x.GetString()!)
            .ToArray();

        // Assert
        Assert.Equal("SearchCards", operation.GetProperty("operationId").GetString());
        Assert.Equal("Search cards on a board", operation.GetProperty("summary").GetString());
        Assert.Contains("All filters must match", operation.GetProperty("description").GetString());
        Assert.Contains("case-sensitive full-value match", operation.GetProperty("description").GetString());
        Assert.Contains("case-insensitive literal substring match", operation.GetProperty("description").GetString());
        Assert.True(successContent.TryGetProperty("$ref", out var successSchemaReference));
        Assert.Contains("ApiResult", successSchemaReference.GetString());
        Assert.True(responses.TryGetProperty("400", out _));
        Assert.True(responses.TryGetProperty("401", out _));
        Assert.True(responses.TryGetProperty("403", out _));
        Assert.Equal([MachinePatScopes.ApiRead], patScopes);
    }

    [Fact]
    public async Task Swagger_CardSearchSchemas_ShouldExposeConstraintsAndExample()
    {
        // Arrange
        var swaggerResponse = await Client.GetAsync("/swagger/v1/swagger.json");
        swaggerResponse.EnsureSuccessStatusCode();
        await using var swaggerStream = await swaggerResponse.Content.ReadAsStreamAsync();
        using var swaggerDocument = await JsonDocument.ParseAsync(swaggerStream);

        // Act
        var schemas = swaggerDocument.RootElement
            .GetProperty("components")
            .GetProperty("schemas");
        var requestSchema = schemas.GetProperty("SearchCardsRequest");
        var requestRequired = requestSchema
            .GetProperty("required")
            .EnumerateArray()
            .Select(x => x.GetString())
            .ToArray();
        var filtersSchema = requestSchema
            .GetProperty("properties")
            .GetProperty("filters");
        var filterSchema = schemas.GetProperty("CardSearchFilterRequest");
        var filterProperties = filterSchema.GetProperty("properties");
        var fieldValues = filterProperties
            .GetProperty("field")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(x => x.GetString()!)
            .ToArray();
        var operatorValues = filterProperties
            .GetProperty("operator")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(x => x.GetString()!)
            .ToArray();
        var exampleFilter = requestSchema
            .GetProperty("example")
            .GetProperty("filters")[0];

        // Assert
        Assert.Contains("filters", requestRequired);
        Assert.Equal(CardSearchLimits.MinimumFilterCount, filtersSchema.GetProperty("minItems").GetInt32());
        Assert.Equal(CardSearchLimits.MaximumFilterCount, filtersSchema.GetProperty("maxItems").GetInt32());
        Assert.Equal([CardSearchFields.ExternalUrl], fieldValues);
        Assert.Equal([CardSearchOperators.Exact, CardSearchOperators.Contains], operatorValues);
        Assert.Equal(1, filterProperties.GetProperty("value").GetProperty("minLength").GetInt32());
        Assert.Equal(CardSearchFields.ExternalUrl, exampleFilter.GetProperty("field").GetString());
        Assert.Equal(CardSearchOperators.Contains, exampleFilter.GetProperty("operator").GetString());
        Assert.False(string.IsNullOrWhiteSpace(exampleFilter.GetProperty("value").GetString()));
    }

}
