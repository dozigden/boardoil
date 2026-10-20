using System.Net;
using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Column;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using BoardOil.Mcp.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpColumnEditIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public McpColumnEditIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    [Fact]
    public async Task Edit_ShouldReturnCompleteOrderedConfigurationAndExposeItThroughExistingReads()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:write"]);
        var before = await StoredColumns();
        var retained = before[0];

        using var payload = await CallAsync(client, token, ToolNames.ColumnEdit, new
        {
            boardId = 1,
            columns = new object[] { new { title = " New first " }, new { id = retained.Id, title = " Retained renamed " } }
        });

        var receipt = AssertSuccess(payload);
        Assert.Equal(["boardId", "columns", "outcome"], receipt.EnumerateObject().Select(property => property.Name));
        Assert.Equal(1, receipt.GetProperty("boardId").GetInt32());
        Assert.Equal("edited", receipt.GetProperty("outcome").GetString());
        var columns = receipt.GetProperty("columns").EnumerateArray().ToArray();
        Assert.All(columns, column => Assert.Equal(["id", "title"], column.EnumerateObject().Select(property => property.Name)));
        Assert.Equal(["New first", "Retained renamed"], columns.Select(column => column.GetProperty("title").GetString()));
        Assert.True(columns[0].GetProperty("id").GetInt32() > 0);
        Assert.DoesNotContain(columns[0].GetProperty("id").GetInt32(), before.Select(column => column.Id));
        Assert.Equal(retained.Id, columns[1].GetProperty("id").GetInt32());
        using var catalogue = await CallAsync(client, token, ToolNames.CardOptionsGet, new { id = 1 });
        Assert.True(JsonElement.DeepEquals(receipt.GetProperty("columns"), AssertSuccess(catalogue).GetProperty("columns")));
        using var board = await CallAsync(client, token, ToolNames.BoardGet, new { id = 1 });
        Assert.Equal(columns.Select(column => column.GetProperty("id").GetInt32()),
            AssertSuccess(board).GetProperty("columns").EnumerateArray().Select(column => column.GetProperty("id").GetInt32()));
    }

    [Fact]
    public async Task Edit_WithReturnedColumns_ShouldRoundTripWithoutChanges()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        using var catalogue = await CallAsync(client, token, ToolNames.CardOptionsGet, new { id = 1 });
        var columns = AssertSuccess(catalogue).GetProperty("columns");
        var before = await StoredColumns();

        using var payload = await CallAsync(client, token, ToolNames.ColumnEdit, new { boardId = 1, columns });

        Assert.True(JsonElement.DeepEquals(columns, AssertSuccess(payload).GetProperty("columns")));
        Assert.Equal(before, await StoredColumns());
    }

    [Theory]
    [InlineData("readScope")]
    [InlineData("contributor")]
    [InlineData("nonMember")]
    public async Task Edit_WithoutWriteScopeOrOwnership_ShouldNotChangeColumns(string restriction)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, [restriction == "readScope" ? "mcp:read" : "mcp:write"]);
        if (restriction != "readScope")
        {
            await using var db = CreateDbContext();
            var membership = await db.BoardMembers.SingleAsync(member => member.BoardId == 1 && member.UserId == 1);
            if (restriction == "contributor") membership.Role = BoardMemberRole.Contributor;
            else db.BoardMembers.Remove(membership);
            await db.SaveChangesAsync();
        }
        var before = await StoredColumns();

        using var payload = await CallAsync(client, token, ToolNames.ColumnEdit, new { boardId = 1, columns = Array.Empty<object>() });

        AssertError(payload, "forbidden", 403);
        Assert.Equal(before, await StoredColumns());
    }

    [Theory]
    [InlineData("{}", "boardId")]
    [InlineData("{\"boardId\":0,\"columns\":[]}", "boardId")]
    [InlineData("{\"boardId\":1}", "columns")]
    [InlineData("{\"boardId\":1,\"columns\":null}", "columns")]
    [InlineData("{\"boardId\":1,\"columns\":[{}]}", "columns[0].title")]
    [InlineData("{\"boardId\":1,\"columns\":[{\"id\":2147483647,\"title\":\"New\"}]}", "columns[0].id")]
    public async Task Edit_WithInvalidArguments_ShouldMapValidationWithoutChanges(string argumentsJson, string field)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var before = await StoredColumns();
        using var arguments = JsonDocument.Parse(argumentsJson);

        using var payload = await CallAsync(client, token, ToolNames.ColumnEdit, arguments.RootElement);

        Assert.True(AssertError(payload, "validation_failed", 400).GetProperty("validationErrors").TryGetProperty(field, out _));
        Assert.Equal(before, await StoredColumns());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Edit_WithUnknownColumnProperty_ShouldRejectWholeRequestWithoutChanges(bool includeValidId)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var before = await StoredColumns();
        var entry = new Dictionary<string, object?>
        {
            ["columnId"] = before[0].Id,
            ["title"] = "Renamed"
        };
        if (includeValidId)
        {
            entry["id"] = before[0].Id;
        }

        using var payload = await CallAsync(client, token, ToolNames.ColumnEdit, new
        {
            boardId = 1,
            columns = new object[] { new { title = "Should not be added" }, entry }
        });

        var error = AssertError(payload, "validation_failed", 400);
        Assert.Contains("columnId", error.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal(before, await StoredColumns());
    }

    [Fact]
    public async Task Edit_WithMalformedArray_ShouldReturnStructuredValidation()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var before = await StoredColumns();

        using var payload = await CallAsync(client, token, ToolNames.ColumnEdit, new { boardId = 1, columns = new[] { "invalid" } });

        AssertError(payload, "validation_failed", 400);
        Assert.Equal(before, await StoredColumns());
    }

    [Theory]
    [InlineData("column_create")]
    [InlineData("column_update")]
    [InlineData("column_reorder")]
    [InlineData("column_delete")]
    public async Task RetiredTool_ShouldReturnToolNotFound(string name)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);

        using var payload = await CallAsync(client, token, name, new { boardId = 1 });

        AssertError(payload, "tool_not_found", 404);
    }

    [Fact]
    public async Task Discovery_ShouldAdvertiseCompleteConfigurationInputAndOrderedResult()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);

        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/list", new { }, "column-edit-discovery", token);
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tool = McpJsonRpcClient.GetToolByName(payload, ToolNames.ColumnEdit);
        var input = tool.GetProperty("inputSchema");
        Assert.Equal(["boardId", "columns"], input.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.False(input.GetProperty("additionalProperties").GetBoolean());
        var item = input.GetProperty("properties").GetProperty("columns").GetProperty("items");
        Assert.Equal(["title"], item.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.False(item.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(["integer", "null"], item.GetProperty("properties").GetProperty("id").GetProperty("type").EnumerateArray().Select(value => value.GetString()));
        var output = tool.GetProperty("outputSchema");
        Assert.Equal(["boardId", "columns", "outcome"], output.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(["edited"], output.GetProperty("properties").GetProperty("outcome").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(["id", "title"], output.GetProperty("properties").GetProperty("columns").GetProperty("items").GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.False(output.GetProperty("additionalProperties").GetBoolean());
    }

    private BoardOilDbContext CreateDbContext() => Factory.Services.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();

    private async Task<ColumnDto[]> StoredColumns()
    {
        await using var db = CreateDbContext();
        return await db.Columns.AsNoTracking().OrderBy(column => column.Id)
            .Select(column => new ColumnDto(column.Id, column.Title, column.SortKey, column.CreatedAtUtc, column.UpdatedAtUtc)).ToArrayAsync();
    }

    private static JsonElement AssertSuccess(JsonDocument payload)
    {
        Assert.False(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        return McpJsonRpcClient.GetStructuredContent(payload);
    }

    private static JsonElement AssertError(JsonDocument payload, string code, int status)
    {
        Assert.True(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var error = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.Equal(status, error.GetProperty("statusCode").GetInt32());
        return error;
    }

    private static async Task<JsonDocument> CallAsync(HttpClient client, string token, string name, object arguments)
    {
        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/call", new { name, arguments }, "column-edit", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await McpJsonRpcClient.ParseJsonAsync(response);
    }
}
