using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Card;
using BoardOil.Ef;
using BoardOil.Mcp.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpBoardReorderIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public McpBoardReorderIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    [Fact]
    public async Task Reorder_WithWriteScope_ShouldReturnReceiptAndExposeExactLayoutThroughBoardGet()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        await using var db = CreateDbContext();
        var columns = await db.Columns.Where(x => x.BoardId == 1).OrderBy(x => x.SortKey).Select(x => x.Id).ToArrayAsync();
        var first = await CreateCardAsync(client, columns[0], "First");
        var second = await CreateCardAsync(client, columns[0], "Second");
        var third = await CreateCardAsync(client, columns[1], "Third");
        var layout = columns.Select((id, index) => new
        {
            columnId = id,
            cardIds = index switch { 0 => new[] { third, first }, 1 => new[] { second }, _ => Array.Empty<int>() }
        }).Reverse().ToArray();

        // Act
        using var payload = await CallAsync(client, token, ToolNames.BoardReorder, new { boardId = 1, columns = layout });

        // Assert
        var receipt = AssertSuccess(payload);
        Assert.Equal(["id", "outcome"], receipt.EnumerateObject().Select(x => x.Name));
        Assert.Equal(1, receipt.GetProperty("id").GetInt32());
        Assert.Equal("reordered", receipt.GetProperty("outcome").GetString());
        using var board = await CallAsync(client, token, ToolNames.BoardGet, new { id = 1 });
        var stored = AssertSuccess(board).GetProperty("columns").EnumerateArray().ToArray();
        Assert.Equal(columns, stored.Select(column => column.GetProperty("id").GetInt32()));
        foreach (var column in stored)
        {
            var expected = layout.Single(item => item.columnId == column.GetProperty("id").GetInt32());
            Assert.Equal(expected.cardIds, column.GetProperty("cards").EnumerateArray().Select(card => card.GetProperty("id").GetInt32()));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reorder_WithoutWriteScopeOrMembership_ShouldReturnForbidden(bool removeMembership)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, [removeMembership ? "mcp:write" : "mcp:read"]);
        if (removeMembership)
        {
            await using var db = CreateDbContext();
            db.BoardMembers.Remove(await db.BoardMembers.SingleAsync(x => x.BoardId == 1 && x.UserId == 1));
            await db.SaveChangesAsync();
        }

        // Act
        using var payload = await CallAsync(client, token, ToolNames.BoardReorder, new { boardId = 1, columns = Array.Empty<object>() });

        // Assert
        Assert.True(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var error = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal("forbidden", error.GetProperty("code").GetString());
        Assert.Equal(403, error.GetProperty("statusCode").GetInt32());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"boardId\":1}")]
    [InlineData("{\"boardId\":1,\"columns\":null}")]
    [InlineData("{\"boardId\":1,\"columns\":[{\"columnId\":1,\"cardIds\":[],\"extra\":true}]}")]
    [InlineData("{\"boardId\":1,\"columns\":[{\"columnId\":1,\"cardIds\":[null]}]}")]
    [InlineData("{\"boardId\":1,\"columns\":[],\"layoutToken\":\"unsupported\"}")]
    public async Task Reorder_WithInvalidInput_ShouldReturnStructuredValidationError(string json)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        using var arguments = JsonDocument.Parse(json);

        // Act
        using var payload = await CallAsync(client, token, ToolNames.BoardReorder, arguments.RootElement);

        // Assert
        Assert.True(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var error = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal("validation_failed", error.GetProperty("code").GetString());
        Assert.Equal(400, error.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task Discovery_ShouldExposeStrictColumnAndCardListsWithoutLayoutToken()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);

        // Act
        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/list", new { }, "board-reorder-discovery", token);
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        var tool = McpJsonRpcClient.GetToolByName(payload, ToolNames.BoardReorder);
        var input = tool.GetProperty("inputSchema");
        Assert.False(input.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(["boardId", "columns"], input.GetProperty("required").EnumerateArray().Select(x => x.GetString()));
        var column = input.GetProperty("properties").GetProperty("columns").GetProperty("items");
        Assert.False(column.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(["columnId", "cardIds"], column.GetProperty("required").EnumerateArray().Select(x => x.GetString()));
        Assert.True(column.GetProperty("properties").GetProperty("cardIds").GetProperty("uniqueItems").GetBoolean());
        Assert.Contains("another reorder", tool.GetProperty("description").GetString(), StringComparison.Ordinal);
    }

    private static JsonElement AssertSuccess(JsonDocument payload)
    {
        Assert.False(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        return McpJsonRpcClient.GetStructuredContent(payload);
    }

    private BoardOilDbContext CreateDbContext() =>
        Factory.Services.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();

    private static async Task<int> CreateCardAsync(HttpClient client, int columnId, string title)
    {
        using var response = await client.PostAsJsonAsync("/api/boards/1/cards", new CreateCardRequest(columnId, title, "Description", []));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>())!.Data!.Id;
    }

    private static async Task<JsonDocument> CallAsync(HttpClient client, string token, string name, object arguments)
    {
        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/call", new { name, arguments }, "board-reorder", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await McpJsonRpcClient.ParseJsonAsync(response);
    }
}
