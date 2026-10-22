using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using BoardOil.Mcp.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpBoardCreateIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public McpBoardCreateIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    [Theory]
    [InlineData(null, null, "")]
    [InlineData("normal", "  Board guidance  ", "Board guidance")]
    public async Task Normal_ShouldCreateOwnedBoardWithApplicationDefaults(
        string? mode, string? description, string expectedDescription)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:write"]);
        await using (var db = CreateDbContext())
        {
            db.BoardMembers.RemoveRange(await db.BoardMembers.Where(member => member.UserId == 1).ToArrayAsync());
            await db.SaveChangesAsync();
        }
        var countBefore = await BoardCountAsync();
        var arguments = new JsonObject { ["name"] = "  New roadmap  " };
        if (mode is not null)
        {
            arguments["mode"] = mode;
        }
        if (description is not null)
        {
            arguments["description"] = description;
        }

        using var payload = await CallAsync(client, token, ToolNames.BoardCreate, arguments);

        var boardId = AssertCreated(payload);
        Assert.NotEqual(1, boardId);
        Assert.Equal(countBefore + 1, await BoardCountAsync());
        using var boardPayload = await CallAsync(client, token, ToolNames.BoardGet, new { id = boardId });
        var board = AssertSuccess(boardPayload);
        Assert.Equal("New roadmap", board.GetProperty("name").GetString());
        Assert.Equal(expectedDescription, board.GetProperty("description").GetString());
        Assert.True(board.GetProperty("settings").GetProperty("slickCohesionModeEnabled").GetBoolean());
        Assert.True(board.GetProperty("settings").GetProperty("cardAttachmentThumbnailsEnabled").GetBoolean());
        var columns = board.GetProperty("columns").EnumerateArray().ToArray();
        Assert.Equal(["Todo", "In Progress", "Done"], columns.Select(column => column.GetProperty("title").GetString()));
        Assert.All(columns, column => Assert.Empty(column.GetProperty("cards").EnumerateArray()));
        await AssertOwnedByActorAsync(boardId);
    }

    [Theory]
    [InlineData(null, "Source guidance")]
    [InlineData("  Clone guidance  ", "Clone guidance")]
    [InlineData("", "")]
    public async Task Clone_ShouldAllowContributorAndApplyOptionalDescription(string? description, string expectedDescription)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:write"]);
        string[] sourceColumns;
        await using (var db = CreateDbContext())
        {
            var source = await db.Boards.SingleAsync(board => board.Id == 1);
            source.Description = "Source guidance";
            source.SlickCohesionModeEnabled = false;
            source.CardAttachmentThumbnailsEnabled = false;
            var member = await db.BoardMembers.SingleAsync(member => member.BoardId == 1 && member.UserId == 1);
            member.Role = BoardMemberRole.Contributor;
            var firstColumn = await db.Columns.Where(column => column.BoardId == 1).OrderBy(column => column.SortKey).FirstAsync();
            firstColumn.Title = "Source backlog";
            await db.SaveChangesAsync();
            sourceColumns = await db.Columns.Where(column => column.BoardId == 1).OrderBy(column => column.SortKey)
                .Select(column => column.Title).ToArrayAsync();
        }
        var countBefore = await BoardCountAsync();
        var arguments = new JsonObject { ["mode"] = "clone", ["sourceBoardId"] = 1, ["name"] = "  Cloned roadmap  " };
        if (description is not null)
        {
            arguments["description"] = description;
        }

        using var payload = await CallAsync(client, token, ToolNames.BoardCreate, arguments);

        var boardId = AssertCreated(payload);
        Assert.NotEqual(1, boardId);
        Assert.Equal(countBefore + 1, await BoardCountAsync());
        using var boardPayload = await CallAsync(client, token, ToolNames.BoardGet, new { id = boardId });
        var board = AssertSuccess(boardPayload);
        Assert.Equal("Cloned roadmap", board.GetProperty("name").GetString());
        Assert.Equal(expectedDescription, board.GetProperty("description").GetString());
        Assert.False(board.GetProperty("settings").GetProperty("slickCohesionModeEnabled").GetBoolean());
        Assert.False(board.GetProperty("settings").GetProperty("cardAttachmentThumbnailsEnabled").GetBoolean());
        var columns = board.GetProperty("columns").EnumerateArray().ToArray();
        Assert.Equal(sourceColumns, columns.Select(column => column.GetProperty("title").GetString()));
        Assert.All(columns, column => Assert.Empty(column.GetProperty("cards").EnumerateArray()));
        await AssertOwnedByActorAsync(boardId);
        await using var verifyDb = CreateDbContext();
        Assert.Equal("Source guidance", (await verifyDb.Boards.SingleAsync(board => board.Id == 1)).Description);
    }

    [Theory]
    [InlineData("normal")]
    [InlineData("clone")]
    public async Task Create_WithReadScope_ShouldNotCreateBoard(string mode)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:read"]);
        var countBefore = await BoardCountAsync();

        using var payload = await CallAsync(client, token, ToolNames.BoardCreate, Arguments(mode));

        AssertError(payload, "forbidden", 403);
        Assert.Equal(countBefore, await BoardCountAsync());
    }

    [Theory]
    [InlineData(false, "forbidden", 403)]
    [InlineData(true, "not_found", 404)]
    public async Task Clone_WithInaccessibleOrMissingSource_ShouldNotCreateBoard(bool missing, string code, int status)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var arguments = Arguments("clone");
        if (missing)
        {
            arguments["sourceBoardId"] = int.MaxValue;
        }
        else
        {
            await using var db = CreateDbContext();
            db.BoardMembers.RemoveRange(await db.BoardMembers.Where(member => member.BoardId == 1).ToArrayAsync());
            await db.SaveChangesAsync();
        }
        var countBefore = await BoardCountAsync();

        using var payload = await CallAsync(client, token, ToolNames.BoardCreate, arguments);

        AssertError(payload, code, status);
        Assert.Equal(countBefore, await BoardCountAsync());
    }

    [Theory]
    [InlineData("{}", "name")]
    [InlineData("{\"name\":null}", "name")]
    [InlineData("{\"name\":\"New\",\"mode\":null}", "mode")]
    [InlineData("{\"name\":\"New\",\"mode\":\"import\"}", "mode")]
    [InlineData("{\"name\":\"New\",\"sourceBoardId\":1}", "sourceBoardId")]
    [InlineData("{\"name\":\"New\",\"sourceBoardId\":null}", "sourceBoardId")]
    [InlineData("{\"name\":\"New\",\"mode\":\"normal\",\"sourceBoardId\":1}", "sourceBoardId")]
    [InlineData("{\"name\":\"New\",\"mode\":\"clone\"}", "sourceBoardId")]
    [InlineData("{\"name\":\"New\",\"mode\":\"clone\",\"sourceBoardId\":null}", "sourceBoardId")]
    [InlineData("{\"name\":\"New\",\"mode\":\"clone\",\"sourceBoardId\":0}", "sourceBoardId")]
    [InlineData("{\"name\":\"New\",\"description\":null}", "description")]
    [InlineData("{\"name\":\"New\",\"mode\":\"clone\",\"sourceBoardId\":1,\"description\":null}", "description")]
    public async Task Create_WithInvalidArguments_ShouldReturnFieldErrorWithoutCreatingBoard(string json, string field)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var countBefore = await BoardCountAsync();
        using var arguments = JsonDocument.Parse(json);

        using var payload = await CallAsync(client, token, ToolNames.BoardCreate, arguments.RootElement);

        var error = AssertError(payload, "validation_failed", 400);
        Assert.True(error.GetProperty("validationErrors").TryGetProperty(field, out _));
        Assert.Equal(countBefore, await BoardCountAsync());
    }

    [Fact]
    public async Task Create_WithUnknownArgument_ShouldRejectWithoutCreatingBoard()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var countBefore = await BoardCountAsync();

        using var payload = await CallAsync(client, token, ToolNames.BoardCreate, new { name = "New board", packageId = 1 });

        var error = AssertError(payload, "validation_failed", 400);
        Assert.Contains("packageId", error.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal(countBefore, await BoardCountAsync());
    }

    [Theory]
    [InlineData("normal")]
    [InlineData("clone")]
    public async Task Create_WithInvalidName_ShouldMapSharedServiceValidation(string mode)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var countBefore = await BoardCountAsync();
        var arguments = Arguments(mode);
        arguments["name"] = "   ";

        using var payload = await CallAsync(client, token, ToolNames.BoardCreate, arguments);

        var error = AssertError(payload, "validation_failed", 400);
        Assert.True(error.GetProperty("validationErrors").TryGetProperty("name", out _));
        Assert.Equal(countBefore, await BoardCountAsync());
    }

    [Fact]
    public async Task Discovery_ShouldAdvertiseNormalAndCloneModesWithSharedCreatedReceipt()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);

        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/list", new { }, "board-create-discovery", token);
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tool = McpJsonRpcClient.GetToolByName(payload, "board_create");
        var input = tool.GetProperty("inputSchema");
        var properties = input.GetProperty("properties");
        Assert.Equal(["mode", "name", "description", "sourceBoardId"], properties.EnumerateObject().Select(property => property.Name));
        Assert.Equal(["name"], input.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.False(input.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(["normal", "clone"], properties.GetProperty("mode").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("normal", properties.GetProperty("mode").GetProperty("default").GetString());
        Assert.Equal("string", properties.GetProperty("description").GetProperty("type").GetString());
        var variants = input.GetProperty("oneOf").EnumerateArray().ToArray();
        Assert.Equal(2, variants.Length);
        Assert.Equal("normal", variants[0].GetProperty("properties").GetProperty("mode").GetProperty("const").GetString());
        Assert.Equal(["sourceBoardId"], variants[0].GetProperty("not").GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("clone", variants[1].GetProperty("properties").GetProperty("mode").GetProperty("const").GetString());
        Assert.Equal(["mode", "sourceBoardId"], variants[1].GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var output = tool.GetProperty("outputSchema");
        Assert.Equal(["id", "outcome"], output.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(["created"], output.GetProperty("properties").GetProperty("outcome").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
    }

    private BoardOilDbContext CreateDbContext() => Factory.Services.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();

    private async Task<int> BoardCountAsync()
    {
        await using var db = CreateDbContext();
        return await db.Boards.CountAsync();
    }

    private async Task AssertOwnedByActorAsync(int boardId)
    {
        await using var db = CreateDbContext();
        var owner = Assert.Single(await db.BoardMembers.Where(member => member.BoardId == boardId).ToArrayAsync());
        Assert.Equal(1, owner.UserId);
        Assert.Equal(BoardMemberRole.Owner, owner.Role);
    }

    private static JsonObject Arguments(string mode)
    {
        var arguments = new JsonObject { ["mode"] = mode, ["name"] = "New board" };
        if (mode == "clone")
        {
            arguments["sourceBoardId"] = 1;
        }
        return arguments;
    }

    private static int AssertCreated(JsonDocument payload)
    {
        var receipt = AssertSuccess(payload);
        Assert.Equal(["id", "outcome"], receipt.EnumerateObject().Select(property => property.Name));
        Assert.Equal("created", receipt.GetProperty("outcome").GetString());
        return receipt.GetProperty("id").GetInt32();
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
        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/call", new { name, arguments }, "board-create", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await McpJsonRpcClient.ParseJsonAsync(response);
    }
}
