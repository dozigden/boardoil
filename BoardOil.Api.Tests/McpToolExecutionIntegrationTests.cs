using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpToolExecutionIntegrationTests : McpIntegrationTestBase
{
    [Fact]
    public async Task ToolsAndMutations_WithValidPatBearerToken_ShouldSucceed()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);

        // Act
        var toolsListResponse = await McpJsonRpcClient.SendRequestAsync(client, "tools/list", new { }, "tools-list", patToken);
        Assert.Equal(HttpStatusCode.OK, toolsListResponse.StatusCode);
        using var toolsListPayload = await McpJsonRpcClient.ParseJsonAsync(toolsListResponse);

        var boardGetResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "board.get",
                arguments = new { id = 1 }
            },
            "board-get",
            patToken);
        Assert.Equal(HttpStatusCode.OK, boardGetResponse.StatusCode);
        using var boardGetPayload = await McpJsonRpcClient.ParseJsonAsync(boardGetResponse);

        var todoColumnId = McpJsonRpcClient.GetStructuredContent(boardGetPayload)
            .GetProperty("columns")
            .EnumerateArray()
            .Single(column => column.GetProperty("title").GetString() == "Todo")
            .GetProperty("id")
            .GetInt32();

        var createResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.create",
                arguments = new
                {
                    boardId = 1,
                    columnId = todoColumnId,
                    title = "API in-process MCP test",
                    description = "",
                    tagNames = Array.Empty<string>()
                }
            },
            "card-create",
            patToken);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var verifyResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "board.get",
                arguments = new { id = 1 }
            },
            "board-verify",
            patToken);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        using var verifyPayload = await McpJsonRpcClient.ParseJsonAsync(verifyResponse);

        // Assert
        var toolNames = toolsListPayload.RootElement
            .GetProperty("result")
            .GetProperty("tools")
            .EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString())
            .ToArray();
        Assert.Contains("board.get", toolNames);
        Assert.Contains("card.get", toolNames);
        Assert.Contains("card.create", toolNames);
        Assert.DoesNotContain("card.move_by_column_name", toolNames);

        var cards = McpJsonRpcClient.GetStructuredContent(verifyPayload)
            .GetProperty("columns")
            .EnumerateArray()
            .SelectMany(column => column.GetProperty("cards").EnumerateArray())
            .ToArray();
        var createdCard = Assert.Single(cards, card => card.GetProperty("title").GetString() == "API in-process MCP test");
        Assert.True(createdCard.TryGetProperty("cardTypeId", out _));
        Assert.True(createdCard.TryGetProperty("cardTypeName", out _));
        Assert.True(createdCard.TryGetProperty("cardTypeEmoji", out _));
        Assert.True(createdCard.TryGetProperty("tags", out _));
    }

    [Fact]
    public async Task BoardList_WithReadScopePat_ShouldReturnAccessibleBoards()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client, ["mcp:read"]);

        // Act
        var boardListResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "board.list",
                arguments = new { }
            },
            "board-list-success",
            patToken);
        Assert.Equal(HttpStatusCode.OK, boardListResponse.StatusCode);
        using var boardListPayload = await McpJsonRpcClient.ParseJsonAsync(boardListResponse);

        // Assert
        var boards = McpJsonRpcClient.GetStructuredContent(boardListPayload)
            .GetProperty("boards")
            .EnumerateArray()
            .ToArray();
        Assert.NotEmpty(boards);

        var board = boards[0];
        Assert.True(board.TryGetProperty("id", out _));
        Assert.True(board.TryGetProperty("name", out _));
        Assert.True(board.TryGetProperty("description", out _));
        Assert.True(board.TryGetProperty("createdAtUtc", out _));
        Assert.True(board.TryGetProperty("updatedAtUtc", out _));
    }

    [Fact]
    public async Task BoardList_WithWriteOnlyPat_ShouldReturnForbiddenError()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client, ["mcp:write"]);

        // Act
        var boardListResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "board.list",
                arguments = new { }
            },
            "board-list-forbidden",
            patToken);
        Assert.Equal(HttpStatusCode.OK, boardListResponse.StatusCode);
        using var boardListPayload = await McpJsonRpcClient.ParseJsonAsync(boardListResponse);

        // Assert
        var result = boardListPayload.RootElement.GetProperty("result");
        Assert.True(result.GetProperty("isError").GetBoolean());
        var structuredContent = result.GetProperty("structuredContent");
        Assert.Equal("forbidden", structuredContent.GetProperty("code").GetString());
        Assert.Equal(403, structuredContent.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task BoardGet_ShouldExcludeDescriptions_ButCardGetReturnsFullDetails()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);

        var boardGetResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "board.get",
                arguments = new { id = 1 }
            },
            "board-get-before-card-create",
            patToken);
        Assert.Equal(HttpStatusCode.OK, boardGetResponse.StatusCode);
        using var boardGetPayload = await McpJsonRpcClient.ParseJsonAsync(boardGetResponse);

        var todoColumnId = McpJsonRpcClient.GetStructuredContent(boardGetPayload)
            .GetProperty("columns")
            .EnumerateArray()
            .Single(column => column.GetProperty("title").GetString() == "Todo")
            .GetProperty("id")
            .GetInt32();

        const string fullDescription = "Full detail test";

        var createResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.create",
                arguments = new
                {
                    boardId = 1,
                    columnId = todoColumnId,
                    title = "Board get description test",
                    description = fullDescription,
                    tagNames = Array.Empty<string>()
                }
            },
            "card-create-description-test",
            patToken);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        using var createPayload = await McpJsonRpcClient.ParseJsonAsync(createResponse);
        var createdCard = McpJsonRpcClient.GetStructuredContent(createPayload).GetProperty("card");
        var createdCardId = createdCard.GetProperty("id").GetInt32();

        var boardVerifyResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "board.get",
                arguments = new { id = 1 }
            },
            "board-get-after-card-create",
            patToken);
        Assert.Equal(HttpStatusCode.OK, boardVerifyResponse.StatusCode);
        using var boardVerifyPayload = await McpJsonRpcClient.ParseJsonAsync(boardVerifyResponse);

        var boardCard = McpJsonRpcClient.GetStructuredContent(boardVerifyPayload)
            .GetProperty("columns")
            .EnumerateArray()
            .SelectMany(column => column.GetProperty("cards").EnumerateArray())
            .Single(card => card.GetProperty("id").GetInt32() == createdCardId);

        Assert.False(boardCard.TryGetProperty("description", out _));

        var cardGetResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.get",
                arguments = new { boardId = 1, id = createdCardId }
            },
            "card-get-description-test",
            patToken);
        Assert.Equal(HttpStatusCode.OK, cardGetResponse.StatusCode);
        using var cardGetPayload = await McpJsonRpcClient.ParseJsonAsync(cardGetResponse);

        var cardData = McpJsonRpcClient.GetStructuredContent(cardGetPayload);
        Assert.Equal(fullDescription, cardData.GetProperty("description").GetString());
    }

    [Fact]
    public async Task BoardSnapshotsAndMutations_ShouldExposeCanonicalIdFields()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);

        // Act
        var boardGetResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "board.get",
                arguments = new { id = 1 }
            },
            "board-get-compat-output",
            patToken);
        Assert.Equal(HttpStatusCode.OK, boardGetResponse.StatusCode);
        using var boardGetPayload = await McpJsonRpcClient.ParseJsonAsync(boardGetResponse);

        var boardData = McpJsonRpcClient.GetStructuredContent(boardGetPayload);
        Assert.True(boardData.TryGetProperty("id", out _));
        Assert.True(boardData.TryGetProperty("description", out _));
        Assert.False(boardData.TryGetProperty("boardId", out _));

        var todoColumn = boardData
            .GetProperty("columns")
            .EnumerateArray()
            .Single(column => column.GetProperty("title").GetString() == "Todo");
        Assert.True(todoColumn.TryGetProperty("id", out _));
        Assert.False(todoColumn.TryGetProperty("columnId", out _));

        var createResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.create",
                arguments = new
                {
                    boardId = 1,
                    columnId = todoColumn.GetProperty("id").GetInt32(),
                    title = "Compat output MCP card",
                    description = "",
                    tagNames = Array.Empty<string>()
                }
            },
            "card-create-compat-output",
            patToken);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        using var createPayload = await McpJsonRpcClient.ParseJsonAsync(createResponse);

        var createdCard = McpJsonRpcClient.GetStructuredContent(createPayload).GetProperty("card");
        Assert.True(createdCard.TryGetProperty("id", out _));
        Assert.True(createdCard.TryGetProperty("columnId", out _));
        Assert.True(createdCard.TryGetProperty("cardTypeId", out _));
        Assert.True(createdCard.TryGetProperty("cardTypeName", out _));
        Assert.True(createdCard.TryGetProperty("cardTypeEmoji", out _));
        Assert.True(createdCard.TryGetProperty("tags", out _));
        Assert.False(createdCard.TryGetProperty("cardId", out _));
        Assert.False(createdCard.TryGetProperty("boardColumnId", out _));

        var verifyResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "board.get",
                arguments = new { id = 1 }
            },
            "board-get-verify-compat-output",
            patToken);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        using var verifyPayload = await McpJsonRpcClient.ParseJsonAsync(verifyResponse);

        var boardCard = McpJsonRpcClient.GetStructuredContent(verifyPayload)
            .GetProperty("columns")
            .EnumerateArray()
            .SelectMany(column => column.GetProperty("cards").EnumerateArray())
            .Single(card => card.GetProperty("title").GetString() == "Compat output MCP card");

        // Assert
        Assert.True(boardCard.TryGetProperty("id", out _));
        Assert.True(boardCard.TryGetProperty("columnId", out _));
        Assert.True(boardCard.TryGetProperty("cardTypeId", out _));
        Assert.True(boardCard.TryGetProperty("cardTypeName", out _));
        Assert.True(boardCard.TryGetProperty("cardTypeEmoji", out _));
        Assert.True(boardCard.TryGetProperty("tags", out _));
        Assert.False(boardCard.TryGetProperty("cardId", out _));
        Assert.False(boardCard.TryGetProperty("boardColumnId", out _));
    }

    [Fact]
    public async Task ToolsAndMutations_WithCanonicalIds_ShouldSucceed()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);

        // Act
        var boardGetResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "board.get",
                arguments = new { id = 1 }
            },
            "board-get-canonical-id",
            patToken);
        Assert.Equal(HttpStatusCode.OK, boardGetResponse.StatusCode);
        using var boardGetPayload = await McpJsonRpcClient.ParseJsonAsync(boardGetResponse);

        var todoColumnId = McpJsonRpcClient.GetStructuredContent(boardGetPayload)
            .GetProperty("columns")
            .EnumerateArray()
            .Single(column => column.GetProperty("title").GetString() == "Todo")
            .GetProperty("id")
            .GetInt32();

        var createResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.create",
                arguments = new
                {
                    boardId = 1,
                    columnId = todoColumnId,
                    title = "Canonical MCP card",
                    description = "created with canonical ids",
                    tagNames = Array.Empty<string>()
                }
            },
            "card-create-canonical-id",
            patToken);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        using var createPayload = await McpJsonRpcClient.ParseJsonAsync(createResponse);

        var createdCard = McpJsonRpcClient.GetStructuredContent(createPayload).GetProperty("card");
        var createdCardId = createdCard.GetProperty("id").GetInt32();
        var createdCardTypeId = createdCard.GetProperty("cardTypeId").GetInt32();

        var updateResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.update",
                arguments = new
                {
                    boardId = 1,
                    id = createdCardId,
                    cardTypeId = createdCardTypeId,
                    title = "Canonical MCP card updated",
                    description = "updated with canonical ids",
                    tagNames = Array.Empty<string>()
                }
            },
            "card-update-canonical-id",
            patToken);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var deleteResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.delete",
                arguments = new
                {
                    boardId = 1,
                    id = createdCardId
                }
            },
            "card-delete-canonical-id",
            patToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task CardUpdate_WithColumnId_ShouldMoveCardToTopOfTargetColumn()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);

        var createColumnResponse = await client.PostAsJsonAsync(
            "/api/boards/1/columns",
            new
            {
                title = "Doing"
            });
        createColumnResponse.EnsureSuccessStatusCode();

        var boardGetResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "board.get",
                arguments = new { id = 1 }
            },
            "board-get-for-card-update-column-id",
            patToken);
        Assert.Equal(HttpStatusCode.OK, boardGetResponse.StatusCode);
        using var boardGetPayload = await McpJsonRpcClient.ParseJsonAsync(boardGetResponse);

        var columns = McpJsonRpcClient.GetStructuredContent(boardGetPayload)
            .GetProperty("columns")
            .EnumerateArray()
            .ToArray();
        var todoColumnId = columns.Single(column => column.GetProperty("title").GetString() == "Todo").GetProperty("id").GetInt32();
        var doingColumnId = columns.Single(column => column.GetProperty("title").GetString() == "Doing").GetProperty("id").GetInt32();

        var existingAResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.create",
                arguments = new
                {
                    boardId = 1,
                    columnId = doingColumnId,
                    title = "Existing A",
                    description = "",
                    tagNames = Array.Empty<string>()
                }
            },
            "card-create-existing-a",
            patToken);
        Assert.Equal(HttpStatusCode.OK, existingAResponse.StatusCode);

        var existingBResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.create",
                arguments = new
                {
                    boardId = 1,
                    columnId = doingColumnId,
                    title = "Existing B",
                    description = "",
                    tagNames = Array.Empty<string>()
                }
            },
            "card-create-existing-b",
            patToken);
        Assert.Equal(HttpStatusCode.OK, existingBResponse.StatusCode);

        var createResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.create",
                arguments = new
                {
                    boardId = 1,
                    columnId = todoColumnId,
                    title = "Move me",
                    description = "",
                    tagNames = Array.Empty<string>()
                }
            },
            "card-create-move-me",
            patToken);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        using var createPayload = await McpJsonRpcClient.ParseJsonAsync(createResponse);

        var createdCard = McpJsonRpcClient.GetStructuredContent(createPayload).GetProperty("card");
        var createdCardId = createdCard.GetProperty("id").GetInt32();
        var createdCardTypeId = createdCard.GetProperty("cardTypeId").GetInt32();

        // Act
        var updateResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.update",
                arguments = new
                {
                    boardId = 1,
                    id = createdCardId,
                    columnId = doingColumnId,
                    cardTypeId = createdCardTypeId,
                    title = "Move me updated",
                    description = "",
                    tagNames = Array.Empty<string>()
                }
            },
            "card-update-with-column-id",
            patToken);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var verifyResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "board.get",
                arguments = new { id = 1 }
            },
            "board-get-after-card-update-column-id",
            patToken);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        using var verifyPayload = await McpJsonRpcClient.ParseJsonAsync(verifyResponse);

        // Assert
        var verifyColumns = McpJsonRpcClient.GetStructuredContent(verifyPayload)
            .GetProperty("columns")
            .EnumerateArray()
            .ToArray();
        var todoCards = verifyColumns
            .Single(column => column.GetProperty("id").GetInt32() == todoColumnId)
            .GetProperty("cards")
            .EnumerateArray()
            .ToArray();
        var doingCards = verifyColumns
            .Single(column => column.GetProperty("id").GetInt32() == doingColumnId)
            .GetProperty("cards")
            .EnumerateArray()
            .ToArray();

        Assert.Empty(todoCards);
        Assert.Equal("Move me updated", doingCards[0].GetProperty("title").GetString());
        Assert.Equal("Existing B", doingCards[1].GetProperty("title").GetString());
        Assert.Equal("Existing A", doingCards[2].GetProperty("title").GetString());
    }

    [Fact]
    public async Task CardUpdate_WhenAssignedUserIdIsOmitted_ShouldPreserveExistingAssignment()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);

        var boardGetResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "board.get",
                arguments = new { id = 1 }
            },
            "board-get-before-assignment-omitted-update",
            patToken);
        Assert.Equal(HttpStatusCode.OK, boardGetResponse.StatusCode);
        using var boardGetPayload = await McpJsonRpcClient.ParseJsonAsync(boardGetResponse);

        var todoColumnId = McpJsonRpcClient.GetStructuredContent(boardGetPayload)
            .GetProperty("columns")
            .EnumerateArray()
            .Single(column => column.GetProperty("title").GetString() == "Todo")
            .GetProperty("id")
            .GetInt32();

        var usersResponse = await client.GetAsync("/api/users");
        usersResponse.EnsureSuccessStatusCode();
        using var usersPayload = JsonDocument.Parse(await usersResponse.Content.ReadAsStringAsync());
        var adminUser = usersPayload.RootElement
            .GetProperty("data")
            .EnumerateArray()
            .Single(user => string.Equals(user.GetProperty("userName").GetString(), "admin", StringComparison.Ordinal));
        var adminUserId = adminUser.GetProperty("id").GetInt32();

        var createResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.create",
                arguments = new
                {
                    boardId = 1,
                    columnId = todoColumnId,
                    cardTypeId = (int?)null,
                    assignedUserId = adminUserId,
                    title = "Assigned by MCP",
                    description = "",
                    tagNames = Array.Empty<string>()
                }
            },
            "card-create-assigned-before-omitted-update",
            patToken);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        using var createPayload = await McpJsonRpcClient.ParseJsonAsync(createResponse);
        var createdCard = McpJsonRpcClient.GetStructuredContent(createPayload).GetProperty("card");
        var createdCardId = createdCard.GetProperty("id").GetInt32();
        var createdCardTypeId = createdCard.GetProperty("cardTypeId").GetInt32();

        // Act
        var updateResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.update",
                arguments = new
                {
                    boardId = 1,
                    id = createdCardId,
                    columnId = todoColumnId,
                    cardTypeId = createdCardTypeId,
                    title = "Assigned by MCP (updated)",
                    description = "",
                    tagNames = Array.Empty<string>()
                }
            },
            "card-update-omitted-assigned-user-id",
            patToken);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var cardGetResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.get",
                arguments = new
                {
                    boardId = 1,
                    id = createdCardId
                }
            },
            "card-get-after-omitted-assigned-user-id-update",
            patToken);
        Assert.Equal(HttpStatusCode.OK, cardGetResponse.StatusCode);
        using var cardGetPayload = await McpJsonRpcClient.ParseJsonAsync(cardGetResponse);

        // Assert
        var cardData = McpJsonRpcClient.GetStructuredContent(cardGetPayload);
        Assert.Equal(adminUserId, cardData.GetProperty("assignedUserId").GetInt32());
        Assert.Equal("admin", cardData.GetProperty("assignedUserName").GetString());
    }

    [Fact]
    public async Task LegacyMutationInputs_ShouldBeRejected()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);

        // Act
        var boardGetLegacyResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "board.get",
                arguments = new { boardId = 1 }
            },
            "board-get-legacy-rejected",
            patToken);
        Assert.Equal(HttpStatusCode.OK, boardGetLegacyResponse.StatusCode);
        using var boardGetLegacyPayload = await McpJsonRpcClient.ParseJsonAsync(boardGetLegacyResponse);

        var createLegacyResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.create",
                arguments = new
                {
                    boardId = 1,
                    boardColumnId = 1,
                    title = "Legacy MCP card",
                    description = "",
                    tagNames = Array.Empty<string>()
                }
            },
            "card-create-legacy-rejected",
            patToken);
        Assert.Equal(HttpStatusCode.OK, createLegacyResponse.StatusCode);
        using var createLegacyPayload = await McpJsonRpcClient.ParseJsonAsync(createLegacyResponse);

        var updateLegacyResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.update",
                arguments = new
                {
                    boardId = 1,
                    cardId = 1,
                    title = "Legacy MCP card updated",
                    description = "",
                    tagNames = Array.Empty<string>()
                }
            },
            "card-update-legacy-rejected",
            patToken);
        Assert.Equal(HttpStatusCode.OK, updateLegacyResponse.StatusCode);
        using var updateLegacyPayload = await McpJsonRpcClient.ParseJsonAsync(updateLegacyResponse);

        var deleteLegacyResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.delete",
                arguments = new
                {
                    boardId = 1,
                    cardId = 1
                }
            },
            "card-delete-legacy-rejected",
            patToken);
        Assert.Equal(HttpStatusCode.OK, deleteLegacyResponse.StatusCode);
        using var deleteLegacyPayload = await McpJsonRpcClient.ParseJsonAsync(deleteLegacyResponse);

        // Assert
        Assert.Equal("validation_failed", McpJsonRpcClient.GetStructuredContent(boardGetLegacyPayload).GetProperty("code").GetString());
        Assert.Equal("validation_failed", McpJsonRpcClient.GetStructuredContent(createLegacyPayload).GetProperty("code").GetString());
        Assert.Equal("validation_failed", McpJsonRpcClient.GetStructuredContent(updateLegacyPayload).GetProperty("code").GetString());
        Assert.Equal("validation_failed", McpJsonRpcClient.GetStructuredContent(deleteLegacyPayload).GetProperty("code").GetString());
    }

    [Fact]
    public async Task MutationInputs_WithUnknownTopLevelFields_ShouldBeRejected()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);

        var boardGetResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "board.get",
                arguments = new { id = 1 }
            },
            "board-get-for-unknown-fields",
            patToken);
        Assert.Equal(HttpStatusCode.OK, boardGetResponse.StatusCode);
        using var boardGetPayload = await McpJsonRpcClient.ParseJsonAsync(boardGetResponse);
        var todoColumnId = McpJsonRpcClient.GetStructuredContent(boardGetPayload)
            .GetProperty("columns")
            .EnumerateArray()
            .Single(column => column.GetProperty("title").GetString() == "Todo")
            .GetProperty("id")
            .GetInt32();

        // Act
        var createResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.create",
                arguments = new
                {
                    boardId = 1,
                    columnId = todoColumnId,
                    boardColumnId = todoColumnId,
                    title = "Unknown field test card",
                    description = "",
                    tagNames = Array.Empty<string>()
                }
            },
            "card-create-unknown-fields",
            patToken);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        using var createPayload = await McpJsonRpcClient.ParseJsonAsync(createResponse);

        // Assert
        var structuredContent = McpJsonRpcClient.GetStructuredContent(createPayload);
        Assert.Equal("validation_failed", structuredContent.GetProperty("code").GetString());
        Assert.Contains("Unknown tool arguments: boardColumnId.", structuredContent.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CardCreate_WhenMultipleIdentifierInputsAreInvalid_ShouldReturnAllValidationErrors()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);

        // Act
        var response = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.create",
                arguments = new
                {
                    boardId = 0,
                    columnId = 0,
                    title = "Invalid create",
                    description = "validation test",
                    tagNames = Array.Empty<string>()
                }
            },
            "card-create-multi-validation",
            patToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        var structuredContent = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal("validation_failed", structuredContent.GetProperty("code").GetString());
        var validationErrors = structuredContent.GetProperty("validationErrors");
        Assert.True(validationErrors.TryGetProperty("boardId", out var boardIdErrors));
        Assert.True(validationErrors.TryGetProperty("columnId", out var columnIdErrors));
        Assert.NotEmpty(boardIdErrors.EnumerateArray());
        Assert.NotEmpty(columnIdErrors.EnumerateArray());
    }

    [Fact]
    public async Task CardUpdate_WhenMultipleIdentifierInputsAreInvalid_ShouldReturnAllValidationErrors()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);

        // Act
        var response = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.update",
                arguments = new
                {
                    boardId = 0,
                    id = 0,
                    cardTypeId = 0,
                    title = "Invalid update",
                    description = "validation test",
                    tagNames = Array.Empty<string>()
                }
            },
            "card-update-multi-validation",
            patToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        var structuredContent = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal("validation_failed", structuredContent.GetProperty("code").GetString());
        var validationErrors = structuredContent.GetProperty("validationErrors");
        Assert.True(validationErrors.TryGetProperty("boardId", out var boardIdErrors));
        Assert.True(validationErrors.TryGetProperty("id", out var idErrors));
        Assert.True(validationErrors.TryGetProperty("cardTypeId", out var cardTypeIdErrors));
        Assert.NotEmpty(boardIdErrors.EnumerateArray());
        Assert.NotEmpty(idErrors.EnumerateArray());
        Assert.NotEmpty(cardTypeIdErrors.EnumerateArray());
    }

    [Fact]
    public async Task CardMove_WhenMultipleInputsAreInvalid_ShouldReturnAllValidationErrors()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);

        // Act
        var response = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.move",
                arguments = new
                {
                    boardId = 0,
                    id = 0,
                    columnId = 0,
                    afterId = 0
                }
            },
            "card-move-multi-validation",
            patToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        var structuredContent = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal("validation_failed", structuredContent.GetProperty("code").GetString());
        var validationErrors = structuredContent.GetProperty("validationErrors");
        Assert.True(validationErrors.TryGetProperty("boardId", out var boardIdErrors));
        Assert.True(validationErrors.TryGetProperty("id", out var idErrors));
        Assert.True(validationErrors.TryGetProperty("columnId", out var columnIdErrors));
        Assert.True(validationErrors.TryGetProperty("afterId", out var afterIdErrors));
        Assert.NotEmpty(boardIdErrors.EnumerateArray());
        Assert.NotEmpty(idErrors.EnumerateArray());
        Assert.NotEmpty(columnIdErrors.EnumerateArray());
        Assert.NotEmpty(afterIdErrors.EnumerateArray());
    }

    [Fact]
    public async Task CardDelete_WhenMultipleIdentifierInputsAreInvalid_ShouldReturnAllValidationErrors()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);

        // Act
        var response = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card.delete",
                arguments = new
                {
                    boardId = 0,
                    id = 0
                }
            },
            "card-delete-multi-validation",
            patToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        var structuredContent = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal("validation_failed", structuredContent.GetProperty("code").GetString());
        var validationErrors = structuredContent.GetProperty("validationErrors");
        Assert.True(validationErrors.TryGetProperty("boardId", out var boardIdErrors));
        Assert.True(validationErrors.TryGetProperty("id", out var idErrors));
        Assert.NotEmpty(boardIdErrors.EnumerateArray());
        Assert.NotEmpty(idErrors.EnumerateArray());
    }
}
