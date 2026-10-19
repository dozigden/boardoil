using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Column;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using BoardOil.Mcp.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpColumnManagementIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public McpColumnManagementIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    [Fact]
    public async Task Lifecycle_ShouldReorderAndDeleteColumnsAddedAndUpdatedThroughMcp()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:write"]);
        var original = await ReadStoredColumnsAsync();
        using var addedA = await CallAsync(client, token, ToolNames.ColumnCreate, new { boardId = 1, title = "Added A" });
        using var addedB = await CallAsync(client, token, ToolNames.ColumnCreate, new { boardId = 1, title = "Added B" });
        var a = AssertSuccess(addedA).GetProperty("id").GetInt32();
        var b = AssertSuccess(addedB).GetProperty("id").GetInt32();
        using var updated = await CallAsync(client, token, ToolNames.ColumnUpdate, new { boardId = 1, id = b, title = "Updated B" });
        AssertSuccess(updated);
        int[] desired = [b, ..original.Select(column => column.Id), a];

        // Act
        using var reordered = await CallAsync(client, token, ToolNames.ColumnReorder, new { boardId = 1, columnIds = desired });
        using var deleted = await CallAsync(client, token, ToolNames.ColumnDelete, new { boardId = 1, id = a });

        // Assert
        AssertReorderReceipt(AssertSuccess(reordered), desired);
        AssertDeleteReceipt(AssertSuccess(deleted), a);
        using var optionsPayload = await CallAsync(client, token, ToolNames.CardOptionsGet, new { id = 1 });
        var columns = AssertSuccess(optionsPayload).GetProperty("columns").EnumerateArray().ToArray();
        Assert.Equal(desired.Where(id => id != a), columns.Select(column => column.GetProperty("id").GetInt32()));
        Assert.Equal("Updated B", columns[0].GetProperty("title").GetString());
        using var boardPayload = await CallAsync(client, token, ToolNames.BoardGet, new { id = 1 });
        Assert.Equal(columns.Select(column => column.GetProperty("id").GetInt32()),
            AssertSuccess(boardPayload).GetProperty("columns").EnumerateArray().Select(column => column.GetProperty("id").GetInt32()));
    }

    [Theory]
    [InlineData(ToolNames.ColumnReorder, "readScope")]
    [InlineData(ToolNames.ColumnReorder, "contributor")]
    [InlineData(ToolNames.ColumnReorder, "nonMember")]
    [InlineData(ToolNames.ColumnDelete, "readScope")]
    [InlineData(ToolNames.ColumnDelete, "contributor")]
    [InlineData(ToolNames.ColumnDelete, "nonMember")]
    public async Task Mutation_WithoutWriteScopeOrOwnerPermission_ShouldNotChangeColumns(string tool, string restriction)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, [restriction == "readScope" ? "mcp:read" : "mcp:write"]);
        var before = await ReadStoredColumnsAsync();
        await using (var db = CreateDbContext())
        {
            var membership = await db.BoardMembers.SingleAsync(member => member.BoardId == 1 && member.UserId == 1);
            if (restriction == "contributor")
            {
                membership.Role = BoardMemberRole.Contributor;
            }
            else if (restriction == "nonMember")
            {
                db.BoardMembers.Remove(membership);
            }
            await db.SaveChangesAsync();
        }

        // Act
        using var payload = await CallAsync(client, token, tool, Arguments(tool, before));

        // Assert
        AssertError(payload, "forbidden", 403);
        Assert.Equal(before, await ReadStoredColumnsAsync());
    }

    [Theory]
    [InlineData(ToolNames.ColumnReorder, "boardId", false)]
    [InlineData(ToolNames.ColumnReorder, "boardId", true)]
    [InlineData(ToolNames.ColumnReorder, "columnIds", false)]
    [InlineData(ToolNames.ColumnReorder, "columnIds", true)]
    [InlineData(ToolNames.ColumnDelete, "boardId", false)]
    [InlineData(ToolNames.ColumnDelete, "boardId", true)]
    [InlineData(ToolNames.ColumnDelete, "id", false)]
    [InlineData(ToolNames.ColumnDelete, "id", true)]
    public async Task Mutation_WithMissingOrInvalidRequiredField_ShouldReturnValidation(string tool, string field, bool invalidValue)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var before = await ReadStoredColumnsAsync();
        var arguments = Arguments(tool, before);
        if (!invalidValue)
        {
            arguments.Remove(field);
        }
        else if (field == "columnIds")
        {
            arguments[field] = null;
        }
        else
        {
            arguments[field] = 0;
        }

        // Act
        using var payload = await CallAsync(client, token, tool, arguments);

        // Assert
        var error = AssertError(payload, "validation_failed", 400);
        Assert.True(error.GetProperty("validationErrors").TryGetProperty(field, out _));
        Assert.Equal(before, await ReadStoredColumnsAsync());
    }

    [Theory]
    [InlineData(ToolNames.ColumnReorder)]
    [InlineData(ToolNames.ColumnDelete)]
    public async Task Mutation_WithForeignColumn_ShouldNotChangeEitherBoard(string tool)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var ownColumns = await ReadStoredColumnsAsync();
        using var response = await client.PostAsJsonAsync("/api/boards", new CreateBoardRequest("Other board"));
        response.EnsureSuccessStatusCode();
        var other = (await response.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>())!.Data!;
        var foreignId = other.Columns[0].Id;
        var arguments = Arguments(tool, ownColumns);
        if (tool == ToolNames.ColumnReorder)
        {
            arguments["columnIds"] = JsonSerializer.SerializeToNode(new[] { foreignId }.Concat(ownColumns.Skip(1).Select(column => column.Id)));
        }
        else
        {
            arguments["id"] = foreignId;
        }
        var before = await ReadStoredColumnsAsync();

        // Act
        using var payload = await CallAsync(client, token, tool, arguments);

        // Assert
        if (tool == ToolNames.ColumnReorder)
        {
            Assert.True(AssertError(payload, "validation_failed", 400).GetProperty("validationErrors").TryGetProperty("columnIds", out _));
        }
        else
        {
            AssertError(payload, "not_found", 404);
        }
        Assert.Equal(before, await ReadStoredColumnsAsync());
    }

    [Theory]
    [InlineData(ToolNames.ColumnReorder)]
    [InlineData(ToolNames.ColumnDelete)]
    public async Task Mutation_WithMissingBoard_ShouldReturnNotFound(string tool)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var before = await ReadStoredColumnsAsync();
        var arguments = Arguments(tool, before);
        arguments["boardId"] = int.MaxValue;

        using var payload = await CallAsync(client, token, tool, arguments);

        AssertError(payload, "not_found", 404);
        Assert.Equal(before, await ReadStoredColumnsAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reorder_WithDuplicateOrStaleIdSet_ShouldPreserveCurrentOrder(bool stale)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var columns = await ReadStoredColumnsAsync();
        var ids = columns.Select(column => column.Id).ToArray();
        if (stale)
        {
            using var deleted = await CallAsync(client, token, ToolNames.ColumnDelete, new { boardId = 1, id = ids[0] });
            AssertSuccess(deleted);
        }
        else
        {
            ids[0] = ids[1];
        }
        var before = await ReadStoredColumnsAsync();

        // Act
        using var payload = await CallAsync(client, token, ToolNames.ColumnReorder, new { boardId = 1, columnIds = ids });

        // Assert
        Assert.True(AssertError(payload, "validation_failed", 400).GetProperty("validationErrors").TryGetProperty("columnIds", out _));
        Assert.Equal(before, await ReadStoredColumnsAsync());
    }

    [Fact]
    public async Task Reorder_WithMalformedArray_ShouldReturnValidationWithoutChanges()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var before = await ReadStoredColumnsAsync();

        using var payload = await CallAsync(client, token, ToolNames.ColumnReorder, new { boardId = 1, columnIds = new[] { "invalid" } });

        AssertError(payload, "validation_failed", 400);
        Assert.Equal(before, await ReadStoredColumnsAsync());
    }

    [Fact]
    public async Task Reorder_WhenAlreadyInRequestedOrder_ShouldReturnReceiptWithoutChanges()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var before = await ReadStoredColumnsAsync();
        var ids = before.OrderBy(column => column.SortKey, StringComparer.Ordinal).Select(column => column.Id).ToArray();

        using var payload = await CallAsync(client, token, ToolNames.ColumnReorder, new { boardId = 1, columnIds = ids });

        AssertReorderReceipt(AssertSuccess(payload), ids);
        Assert.Equal(before, await ReadStoredColumnsAsync());
    }

    [Fact]
    public async Task Delete_WhenRepeated_ShouldRemainSuccessful()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var target = (await ReadStoredColumnsAsync())[0];
        using var first = await CallAsync(client, token, ToolNames.ColumnDelete, new { boardId = 1, id = target.Id });
        AssertDeleteReceipt(AssertSuccess(first), target.Id);
        var before = await ReadStoredColumnsAsync();

        using var repeated = await CallAsync(client, token, ToolNames.ColumnDelete, new { boardId = 1, id = target.Id });

        AssertDeleteReceipt(AssertSuccess(repeated), target.Id);
        Assert.Equal(before, await ReadStoredColumnsAsync());
        Assert.DoesNotContain(before, column => column.Id == target.Id);
    }

    [Fact]
    public async Task Delete_WithLiveCard_ShouldReturnClearValidationAndPreserveCardAndComments()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var beforeColumns = await ReadStoredColumnsAsync();
        var columnId = beforeColumns[0].Id;
        var cardId = await AddCardAsync(client, columnId);
        using var comment = await client.PostAsJsonAsync($"/api/boards/1/cards/{cardId}/comments", new CreateCardCommentRequest("Keep comment"));
        comment.EnsureSuccessStatusCode();
        using var beforeCard = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id = cardId });

        using var payload = await CallAsync(client, token, ToolNames.ColumnDelete, new { boardId = 1, id = columnId });

        var error = AssertError(payload, "validation_failed", 400);
        Assert.Equal("Column must be empty before it can be deleted.", error.GetProperty("message").GetString());
        Assert.Equal(["Column must be empty before it can be deleted."], error.GetProperty("validationErrors").GetProperty("id").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(beforeColumns, await ReadStoredColumnsAsync());
        using var afterCard = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id = cardId });
        Assert.True(JsonElement.DeepEquals(AssertSuccess(beforeCard), AssertSuccess(afterCard)));
    }

    [Fact]
    public async Task Delete_WithArchivedCard_ShouldSucceedAndPreserveArchive()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var columnId = (await ReadStoredColumnsAsync())[0].Id;
        var cardId = await AddCardAsync(client, columnId);
        using var archived = await client.PostAsync($"/api/boards/1/cards/{cardId}/archive", null);
        archived.EnsureSuccessStatusCode();
        using var before = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id = cardId, archived = true });
        var storedSnapshot = await ReadArchiveSnapshotAsync(cardId);

        using var deleted = await CallAsync(client, token, ToolNames.ColumnDelete, new { boardId = 1, id = columnId });

        AssertDeleteReceipt(AssertSuccess(deleted), columnId);
        Assert.DoesNotContain(await ReadStoredColumnsAsync(), column => column.Id == columnId);
        Assert.Equal(storedSnapshot, await ReadArchiveSnapshotAsync(cardId));
        using var after = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id = cardId, archived = true });
        var beforeContent = JsonNode.Parse(AssertSuccess(before).GetRawText())!.AsObject();
        var afterContent = JsonNode.Parse(AssertSuccess(after).GetRawText())!.AsObject();
        // Archived reads resolve the current restore column rather than retaining its old ID.
        beforeContent.Remove("columnId");
        afterContent.Remove("columnId");
        Assert.True(JsonNode.DeepEquals(beforeContent, afterContent));
    }

    [Fact]
    public async Task DeleteLastColumnAndReorderEmptyBoard_ShouldSucceed()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var columns = await ReadStoredColumnsAsync();
        foreach (var column in columns.Skip(1))
        {
            using var removed = await client.DeleteAsync($"/api/boards/1/columns/{column.Id}");
            removed.EnsureSuccessStatusCode();
        }

        using var deleted = await CallAsync(client, token, ToolNames.ColumnDelete, new { boardId = 1, id = columns[0].Id });
        using var reordered = await CallAsync(client, token, ToolNames.ColumnReorder, new { boardId = 1, columnIds = Array.Empty<int>() });

        AssertDeleteReceipt(AssertSuccess(deleted), columns[0].Id);
        AssertReorderReceipt(AssertSuccess(reordered), []);
        Assert.Empty(await ReadStoredColumnsAsync());
    }

    [Theory]
    [InlineData(ToolNames.ColumnReorder)]
    [InlineData(ToolNames.ColumnDelete)]
    public async Task Discovery_ShouldAdvertiseRequiredInputsAndReorderReceipt(string toolName)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:write"]);

        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/list", new { }, "column-management-discovery", token);
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tool = McpJsonRpcClient.GetToolByName(payload, toolName);
        var input = tool.GetProperty("inputSchema");
        Assert.False(input.GetProperty("additionalProperties").GetBoolean());
        if (toolName == ToolNames.ColumnReorder)
        {
            Assert.Equal(["boardId", "columnIds"], input.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
            var ids = input.GetProperty("properties").GetProperty("columnIds");
            Assert.Equal("array", ids.GetProperty("type").GetString());
            Assert.True(ids.GetProperty("uniqueItems").GetBoolean());
            Assert.Equal("integer", ids.GetProperty("items").GetProperty("type").GetString());
            Assert.Equal(1, ids.GetProperty("items").GetProperty("minimum").GetInt32());
            var output = tool.GetProperty("outputSchema");
            Assert.False(output.GetProperty("additionalProperties").GetBoolean());
            Assert.Equal(["boardId", "columnIds", "outcome"], output.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
            Assert.Equal(["reordered"], output.GetProperty("properties").GetProperty("outcome").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        }
        else
        {
            Assert.Equal(["boardId", "id"], input.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
            Assert.Equal(1, input.GetProperty("properties").GetProperty("id").GetProperty("minimum").GetInt32());
        }
    }

    private static JsonObject Arguments(string tool, IReadOnlyList<ColumnDto> columns)
    {
        var arguments = new JsonObject { ["boardId"] = 1 };
        if (tool == ToolNames.ColumnReorder)
        {
            arguments["columnIds"] = JsonSerializer.SerializeToNode(columns.Reverse().Select(column => column.Id));
        }
        else
        {
            arguments["id"] = columns[0].Id;
        }
        return arguments;
    }

    private static async Task<int> AddCardAsync(HttpClient client, int columnId)
    {
        using var response = await client.PostAsJsonAsync("/api/boards/1/cards", new CreateCardRequest(columnId, "Keep card", "Keep content", []));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>())!.Data!.Id;
    }

    private BoardOilDbContext CreateDbContext() => Factory.Services.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();

    private async Task<ColumnDto[]> ReadStoredColumnsAsync()
    {
        await using var db = CreateDbContext();
        return await db.Columns.OrderBy(column => column.Id).Select(column => new ColumnDto(
            column.Id, column.Title, column.SortKey, column.CreatedAtUtc, column.UpdatedAtUtc)).ToArrayAsync();
    }

    private async Task<string> ReadArchiveSnapshotAsync(int cardId)
    {
        await using var db = CreateDbContext();
        return await db.ArchivedCards.Where(card => card.BoardId == 1 && card.OriginalCardId == cardId)
            .Select(card => card.SnapshotJson).SingleAsync();
    }

    private static void AssertReorderReceipt(JsonElement receipt, IEnumerable<int> ids)
    {
        Assert.Equal(["boardId", "columnIds", "outcome"], receipt.EnumerateObject().Select(property => property.Name));
        Assert.Equal(1, receipt.GetProperty("boardId").GetInt32());
        Assert.Equal(ids, receipt.GetProperty("columnIds").EnumerateArray().Select(value => value.GetInt32()));
        Assert.Equal("reordered", receipt.GetProperty("outcome").GetString());
    }

    private static void AssertDeleteReceipt(JsonElement receipt, int id)
    {
        Assert.Equal(["id", "outcome"], receipt.EnumerateObject().Select(property => property.Name));
        Assert.Equal(id, receipt.GetProperty("id").GetInt32());
        Assert.Equal("deleted", receipt.GetProperty("outcome").GetString());
    }

    private static JsonElement AssertSuccess(JsonDocument payload)
    {
        Assert.False(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        return McpJsonRpcClient.GetStructuredContent(payload);
    }

    private static JsonElement AssertError(JsonDocument payload, string code, int statusCode)
    {
        Assert.True(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var error = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.Equal(statusCode, error.GetProperty("statusCode").GetInt32());
        return error;
    }

    private static async Task<JsonDocument> CallAsync(HttpClient client, string token, string tool, object arguments)
    {
        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/call", new { name = tool, arguments }, "column-management", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await McpJsonRpcClient.ParseJsonAsync(response);
    }
}
