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

public sealed class McpColumnUpdateIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public McpColumnUpdateIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_ShouldReturnReceiptAndPreserveOrderAndCards(bool duplicateTitle)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:write"]);
        var before = await ReadStoredColumnsAsync();
        var target = before[0];
        var title = duplicateTitle ? before[1].Title : "Updated column";
        using var createdCard = await client.PostAsJsonAsync("/api/boards/1/cards", new CreateCardRequest(target.Id, "Keep card", "Keep content", []));
        createdCard.EnsureSuccessStatusCode();
        using var beforeBoard = await CallAsync(client, token, ToolNames.BoardGet, new { id = 1 });
        var beforeColumns = AssertSuccess(beforeBoard).GetProperty("columns").EnumerateArray().ToArray();

        // Act
        using var payload = await CallAsync(client, token, ToolNames.ColumnUpdate, new { boardId = 1, id = target.Id, title = $"  {title}  " });

        // Assert
        var output = AssertSuccess(payload);
        Assert.Equal(["id", "outcome"], output.EnumerateObject().Select(property => property.Name));
        Assert.Equal(target.Id, output.GetProperty("id").GetInt32());
        Assert.Equal("updated", output.GetProperty("outcome").GetString());
        var stored = await ReadStoredColumnsAsync();
        Assert.Equal(before.Select(column => column.Id), stored.Select(column => column.Id));
        Assert.Equal(before.Select(column => column.SortKey), stored.Select(column => column.SortKey));
        Assert.Equal(title, stored.Single(column => column.Id == target.Id).Title);
        Assert.Equal(before.Skip(1), stored.Skip(1));
        using var optionsPayload = await CallAsync(client, token, ToolNames.CardOptionsGet, new { id = 1 });
        var option = AssertSuccess(optionsPayload).GetProperty("columns").EnumerateArray().Single(column => column.GetProperty("id").GetInt32() == target.Id);
        Assert.Equal(title, option.GetProperty("title").GetString());
        using var afterBoard = await CallAsync(client, token, ToolNames.BoardGet, new { id = 1 });
        var afterColumns = AssertSuccess(afterBoard).GetProperty("columns").EnumerateArray().ToArray();
        Assert.Equal(beforeColumns.Select(column => column.GetProperty("id").GetInt32()), afterColumns.Select(column => column.GetProperty("id").GetInt32()));
        for (var index = 0; index < beforeColumns.Length; index++)
        {
            Assert.True(JsonElement.DeepEquals(beforeColumns[index].GetProperty("cards"), afterColumns[index].GetProperty("cards")));
        }
    }

    [Theory]
    [InlineData("title", false)]
    [InlineData("title", true)]
    [InlineData("boardId", false)]
    [InlineData("boardId", true)]
    [InlineData("id", false)]
    [InlineData("id", true)]
    public async Task Update_WithMissingOrInvalidRequiredField_ShouldNotChangeColumns(string field, bool invalidValue)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var before = await ReadStoredColumnsAsync();
        var arguments = new JsonObject { ["boardId"] = 1, ["id"] = before[0].Id, ["title"] = "Updated" };
        if (!invalidValue)
        {
            arguments.Remove(field);
        }
        else if (field == "title")
        {
            arguments[field] = null;
        }
        else
        {
            arguments[field] = 0;
        }

        // Act
        using var payload = await CallAsync(client, token, ToolNames.ColumnUpdate, arguments);

        // Assert
        var error = AssertError(payload, "validation_failed", 400);
        Assert.True(error.GetProperty("validationErrors").TryGetProperty(field, out _));
        Assert.Equal(before, await ReadStoredColumnsAsync());
    }

    [Fact]
    public async Task Update_WithInvalidTitle_ShouldMapServiceFieldErrorWithoutChanges()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var before = await ReadStoredColumnsAsync();

        // Act
        using var payload = await CallAsync(client, token, ToolNames.ColumnUpdate, new { boardId = 1, id = before[0].Id, title = "bad@title" });

        // Assert
        var error = AssertError(payload, "validation_failed", 400);
        Assert.True(error.GetProperty("validationErrors").TryGetProperty("title", out _));
        Assert.Equal(before, await ReadStoredColumnsAsync());
    }

    [Theory]
    [InlineData("readScope")]
    [InlineData("contributor")]
    [InlineData("nonMember")]
    public async Task Update_WithoutWriteScopeOrOwnerPermission_ShouldNotChangeColumns(string restriction)
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
        using var payload = await CallAsync(client, token, ToolNames.ColumnUpdate, new { boardId = 1, id = before[0].Id, title = "Forbidden" });

        // Assert
        AssertError(payload, "forbidden", 403);
        Assert.Equal(before, await ReadStoredColumnsAsync());
    }

    [Theory]
    [InlineData("column")]
    [InlineData("board")]
    [InlineData("foreign")]
    public async Task Update_WithMissingOrForeignTarget_ShouldReturnNotFoundWithoutChanges(string missing)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var boardId = 1;
        var id = (await ReadStoredColumnsAsync())[0].Id;
        if (missing == "column")
        {
            id = int.MaxValue;
        }
        else if (missing == "board")
        {
            boardId = int.MaxValue;
        }
        else
        {
            using var response = await client.PostAsJsonAsync("/api/boards", new CreateBoardRequest("Other board"));
            response.EnsureSuccessStatusCode();
            var other = (await response.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>())!.Data!;
            id = other.Columns[0].Id;
        }
        var before = await ReadStoredColumnsAsync();

        // Act
        using var payload = await CallAsync(client, token, ToolNames.ColumnUpdate, new { boardId, id, title = "Wrong target" });

        // Assert
        AssertError(payload, "not_found", 404);
        Assert.Equal(before, await ReadStoredColumnsAsync());
    }

    [Fact]
    public async Task Update_WhenTitleAlreadyMatches_ShouldSucceedWithoutChangingStoredColumn()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var before = await ReadStoredColumnsAsync();
        var target = before[0];

        // Act
        using var payload = await CallAsync(client, token, ToolNames.ColumnUpdate, new { boardId = 1, id = target.Id, title = target.Title });

        // Assert
        var output = AssertSuccess(payload);
        Assert.Equal(target.Id, output.GetProperty("id").GetInt32());
        Assert.Equal("updated", output.GetProperty("outcome").GetString());
        Assert.Equal(before, await ReadStoredColumnsAsync());
    }

    [Fact]
    public async Task Discovery_ShouldAdvertiseRequiredInputs()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:write"]);

        // Act
        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/list", new { }, "column-update-discovery", token);
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tool = McpJsonRpcClient.GetToolByName(payload, ToolNames.ColumnUpdate);
        Assert.Equal("Update a column.", tool.GetProperty("description").GetString());
        var input = tool.GetProperty("inputSchema");
        Assert.Equal(["boardId", "id", "title"], input.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.False(input.GetProperty("additionalProperties").GetBoolean());
        var properties = input.GetProperty("properties");
        Assert.Equal(["boardId", "id", "title"], properties.EnumerateObject().Select(property => property.Name));
        Assert.Equal("integer", properties.GetProperty("id").GetProperty("type").GetString());
        Assert.Equal(1, properties.GetProperty("id").GetProperty("minimum").GetInt32());
        Assert.Equal("string", properties.GetProperty("title").GetProperty("type").GetString());
        Assert.Equal(200, properties.GetProperty("title").GetProperty("maxLength").GetInt32());
    }

    private BoardOilDbContext CreateDbContext() => Factory.Services.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();

    private async Task<ColumnDto[]> ReadStoredColumnsAsync()
    {
        await using var db = CreateDbContext();
        return await db.Columns.OrderBy(column => column.Id).Select(column => new ColumnDto(
            column.Id, column.Title, column.SortKey, column.CreatedAtUtc, column.UpdatedAtUtc)).ToArrayAsync();
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

    private static async Task<JsonDocument> CallAsync(HttpClient client, string token, string toolName, object arguments)
    {
        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/call", new { name = toolName, arguments }, "column-update", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await McpJsonRpcClient.ParseJsonAsync(response);
    }
}
