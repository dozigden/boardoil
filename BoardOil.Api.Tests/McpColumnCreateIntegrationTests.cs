using System.Net;
using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using BoardOil.Mcp.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpColumnCreateIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public McpColumnCreateIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_ShouldReturnReceiptAndAddTrimmedTitleAtEnd(bool duplicateTitle)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:write"]);
        using var beforePayload = await CallAsync(client, token, ToolNames.CardOptionsGet, new { id = 1 });
        var before = AssertSuccess(beforePayload).GetProperty("columns").EnumerateArray().ToArray();
        var title = duplicateTitle ? before[0].GetProperty("title").GetString()! : "Added column";

        // Act
        using var payload = await CallAsync(client, token, ToolNames.ColumnCreate, new { boardId = 1, title = $"  {title}  " });

        // Assert
        var output = AssertSuccess(payload);
        Assert.Equal(["id", "outcome"], output.EnumerateObject().Select(property => property.Name));
        var id = output.GetProperty("id").GetInt32();
        Assert.True(id > 0);
        Assert.Equal("created", output.GetProperty("outcome").GetString());
        using var optionsPayload = await CallAsync(client, token, ToolNames.CardOptionsGet, new { id = 1 });
        var columns = AssertSuccess(optionsPayload).GetProperty("columns").EnumerateArray().ToArray();
        Assert.Equal(before.Length + 1, columns.Length);
        Assert.Equal(before.Select(column => column.GetProperty("id").GetInt32()),
            columns.Take(before.Length).Select(column => column.GetProperty("id").GetInt32()));
        Assert.Equal(id, columns[^1].GetProperty("id").GetInt32());
        Assert.Equal(title, columns[^1].GetProperty("title").GetString());
        using var boardPayload = await CallAsync(client, token, ToolNames.BoardGet, new { id = 1 });
        var last = AssertSuccess(boardPayload).GetProperty("columns").EnumerateArray().Last();
        Assert.Equal(id, last.GetProperty("id").GetInt32());
        Assert.Equal(title, last.GetProperty("title").GetString());
        Assert.Empty(last.GetProperty("cards").EnumerateArray());
    }

    [Theory]
    [InlineData("{\"boardId\":1}", "title")]
    [InlineData("{\"boardId\":1,\"title\":null}", "title")]
    [InlineData("{\"title\":\"Added\"}", "boardId")]
    [InlineData("{\"boardId\":0,\"title\":\"Added\"}", "boardId")]
    public async Task Create_WithMissingOrInvalidRequiredField_ShouldReturnValidationWithoutAddingColumn(string json, string field)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var before = await CountColumnsAsync();
        using var arguments = JsonDocument.Parse(json);

        // Act
        using var payload = await CallAsync(client, token, ToolNames.ColumnCreate, arguments.RootElement);

        // Assert
        var error = AssertError(payload, "validation_failed", 400);
        Assert.True(error.GetProperty("validationErrors").TryGetProperty(field, out _));
        Assert.Equal(before, await CountColumnsAsync());
    }

    [Fact]
    public async Task Create_WithInvalidTitle_ShouldMapServiceFieldError()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var before = await CountColumnsAsync();

        // Act
        using var payload = await CallAsync(client, token, ToolNames.ColumnCreate, new { boardId = 1, title = "bad@title" });

        // Assert
        var error = AssertError(payload, "validation_failed", 400);
        Assert.True(error.GetProperty("validationErrors").TryGetProperty("title", out _));
        Assert.Equal(before, await CountColumnsAsync());
    }

    [Theory]
    [InlineData("readScope")]
    [InlineData("contributor")]
    [InlineData("nonMember")]
    public async Task Create_WithoutWriteScopeOrOwnerPermission_ShouldReturnForbidden(string restriction)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, [restriction == "readScope" ? "mcp:read" : "mcp:write"]);
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
        var before = await CountColumnsAsync();

        // Act
        using var payload = await CallAsync(client, token, ToolNames.ColumnCreate, new { boardId = 1, title = "Forbidden" });

        // Assert
        AssertError(payload, "forbidden", 403);
        Assert.Equal(before, await CountColumnsAsync());
    }

    [Fact]
    public async Task Create_WithMissingBoard_ShouldReturnNotFound()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var before = await CountColumnsAsync();

        // Act
        using var payload = await CallAsync(client, token, ToolNames.ColumnCreate, new { boardId = int.MaxValue, title = "Missing board" });

        // Assert
        AssertError(payload, "not_found", 404);
        Assert.Equal(before, await CountColumnsAsync());
    }

    [Fact]
    public async Task Discovery_ShouldAdvertiseRequiredInputs()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:write"]);

        // Act
        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/list", new { }, "column-create-discovery", token);
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tool = McpJsonRpcClient.GetToolByName(payload, ToolNames.ColumnCreate);
        Assert.Equal("Add a column.", tool.GetProperty("description").GetString());
        var input = tool.GetProperty("inputSchema");
        Assert.Equal(["boardId", "title"], input.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.False(input.GetProperty("additionalProperties").GetBoolean());
        var properties = input.GetProperty("properties");
        Assert.Equal(["boardId", "title"], properties.EnumerateObject().Select(property => property.Name));
        Assert.Equal("integer", properties.GetProperty("boardId").GetProperty("type").GetString());
        Assert.Equal(1, properties.GetProperty("boardId").GetProperty("minimum").GetInt32());
        var title = properties.GetProperty("title");
        Assert.Equal("string", title.GetProperty("type").GetString());
        Assert.Equal(1, title.GetProperty("minLength").GetInt32());
        Assert.Equal(200, title.GetProperty("maxLength").GetInt32());
    }

    private BoardOilDbContext CreateDbContext() => Factory.Services.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();

    private async Task<int> CountColumnsAsync()
    {
        await using var db = CreateDbContext();
        return await db.Columns.CountAsync();
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
        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/call", new { name = toolName, arguments }, "column-create", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await McpJsonRpcClient.ParseJsonAsync(response);
    }
}
