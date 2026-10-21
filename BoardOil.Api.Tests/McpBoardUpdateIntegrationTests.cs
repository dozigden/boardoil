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

public sealed class McpBoardUpdateIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public McpBoardUpdateIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    [Theory]
    [InlineData(false, true, "  Board guidance  ", "Board guidance")]
    [InlineData(true, false, "", "")]
    public async Task Update_ShouldReturnReceiptAndPersistCompleteDefinition(
        bool slickCohesion, bool thumbnails, string description, string expectedDescription)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:write"]);
        await using (var db = CreateDbContext())
        {
            var storedBoard = await db.Boards.SingleAsync(board => board.Id == 1);
            storedBoard.Description = "Previous description";
            await db.SaveChangesAsync();
        }
        var arguments = Arguments();
        arguments["name"] = "  MCP roadmap  ";
        arguments["description"] = description;
        arguments["settings"]!["slickCohesionModeEnabled"] = slickCohesion;
        arguments["settings"]!["cardAttachmentThumbnailsEnabled"] = thumbnails;

        using var payload = await CallAsync(client, token, ToolNames.BoardUpdate, arguments);

        var receipt = AssertSuccess(payload);
        Assert.Equal(["id", "outcome"], receipt.EnumerateObject().Select(property => property.Name));
        Assert.Equal(1, receipt.GetProperty("id").GetInt32());
        Assert.Equal("updated", receipt.GetProperty("outcome").GetString());
        var stored = await StoredBoardAsync();
        Assert.Equal("MCP roadmap", stored.Name);
        Assert.Equal(expectedDescription, stored.Description);
        Assert.Equal(slickCohesion, stored.SlickCohesionModeEnabled);
        Assert.Equal(thumbnails, stored.CardAttachmentThumbnailsEnabled);
        using var board = await CallAsync(client, token, ToolNames.BoardGet, new { id = 1 });
        var snapshot = AssertSuccess(board);
        Assert.Equal(["id", "name", "description", "settings", "updatedAtUtc", "columns"],
            snapshot.EnumerateObject().Select(property => property.Name));
        Assert.Equal(stored.Name, snapshot.GetProperty("name").GetString());
        Assert.Equal(stored.Description, snapshot.GetProperty("description").GetString());
        Assert.Equal(slickCohesion, snapshot.GetProperty("settings").GetProperty("slickCohesionModeEnabled").GetBoolean());
        Assert.Equal(thumbnails, snapshot.GetProperty("settings").GetProperty("cardAttachmentThumbnailsEnabled").GetBoolean());
        Assert.NotEmpty(snapshot.GetProperty("columns").EnumerateArray());
    }

    [Fact]
    public async Task Update_WithBoardGetValues_ShouldRoundTripWithoutChanges()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        using var board = await CallAsync(client, token, ToolNames.BoardGet, new { id = 1 });
        var snapshot = AssertSuccess(board);
        var before = await StoredBoardAsync();

        using var payload = await CallAsync(client, token, ToolNames.BoardUpdate, new
        {
            boardId = snapshot.GetProperty("id"),
            name = snapshot.GetProperty("name"),
            description = snapshot.GetProperty("description"),
            settings = snapshot.GetProperty("settings")
        });

        AssertSuccess(payload);
        Assert.Equal(before, await StoredBoardAsync());
    }

    [Fact]
    public async Task Get_WithReadScope_ShouldExposeSettingsAndLeaveBoardListUnchanged()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:read"]);
        await using (var db = CreateDbContext())
        {
            var board = await db.Boards.SingleAsync(board => board.Id == 1);
            board.SlickCohesionModeEnabled = false;
            board.CardAttachmentThumbnailsEnabled = false;
            await db.SaveChangesAsync();
        }

        using var boardPayload = await CallAsync(client, token, ToolNames.BoardGet, new { id = 1 });
        using var listPayload = await CallAsync(client, token, ToolNames.BoardList, new { });

        var settings = AssertSuccess(boardPayload).GetProperty("settings");
        Assert.Equal(["slickCohesionModeEnabled", "cardAttachmentThumbnailsEnabled"],
            settings.EnumerateObject().Select(property => property.Name));
        Assert.False(settings.GetProperty("slickCohesionModeEnabled").GetBoolean());
        Assert.False(settings.GetProperty("cardAttachmentThumbnailsEnabled").GetBoolean());
        var summary = Assert.Single(AssertSuccess(listPayload).GetProperty("boards").EnumerateArray());
        Assert.Equal(["id", "name", "description", "createdAtUtc", "updatedAtUtc", "currentUserRole"],
            summary.EnumerateObject().Select(property => property.Name));
    }

    [Theory]
    [InlineData("readScope")]
    [InlineData("contributor")]
    [InlineData("nonMember")]
    public async Task Update_WithoutWriteScopeOrOwnership_ShouldNotChangeBoard(string restriction)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, [restriction == "readScope" ? "mcp:read" : "mcp:write"]);
        if (restriction != "readScope")
        {
            await using var db = CreateDbContext();
            var member = await db.BoardMembers.SingleAsync(member => member.BoardId == 1 && member.UserId == 1);
            if (restriction == "contributor")
            {
                member.Role = BoardMemberRole.Contributor;
            }
            else
            {
                db.BoardMembers.Remove(member);
            }
            await db.SaveChangesAsync();
        }
        var before = await StoredBoardAsync();

        using var payload = await CallAsync(client, token, ToolNames.BoardUpdate, Arguments());

        AssertError(payload, "forbidden", 403);
        Assert.Equal(before, await StoredBoardAsync());
    }

    [Theory]
    [InlineData("boardId", false)]
    [InlineData("name", false)]
    [InlineData("name", true)]
    [InlineData("description", false)]
    [InlineData("description", true)]
    [InlineData("settings", false)]
    [InlineData("settings", true)]
    [InlineData("settings.slickCohesionModeEnabled", false)]
    [InlineData("settings.slickCohesionModeEnabled", true)]
    [InlineData("settings.cardAttachmentThumbnailsEnabled", false)]
    [InlineData("settings.cardAttachmentThumbnailsEnabled", true)]
    public async Task Update_WithMissingRequiredValue_ShouldReturnFieldErrorWithoutChanges(string field, bool explicitNull)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var before = await StoredBoardAsync();
        var arguments = Arguments();
        var fieldParts = field.Split('.');
        var parent = arguments;
        if (fieldParts.Length > 1)
        {
            parent = arguments[fieldParts[0]]!.AsObject();
        }
        if (explicitNull)
        {
            parent[fieldParts[^1]] = null;
        }
        else
        {
            parent.Remove(fieldParts[^1]);
        }

        using var payload = await CallAsync(client, token, ToolNames.BoardUpdate, arguments);

        var error = AssertError(payload, "validation_failed", 400);
        Assert.True(error.GetProperty("validationErrors").TryGetProperty(field, out _));
        Assert.Equal(before, await StoredBoardAsync());
    }

    [Theory]
    [InlineData("{\"slickCohesionModeEnabled\":false,\"cardAttachmentThumbnailsEnabled\":false,\"unknownSetting\":true}")]
    [InlineData("{\"slickCohesionModeEnabled\":\"false\",\"cardAttachmentThumbnailsEnabled\":false}")]
    public async Task Update_WithInvalidSettingsShape_ShouldRejectWithoutChanges(string settingsJson)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var before = await StoredBoardAsync();
        var arguments = Arguments();
        arguments["settings"] = JsonNode.Parse(settingsJson);

        using var payload = await CallAsync(client, token, ToolNames.BoardUpdate, arguments);

        AssertError(payload, "validation_failed", 400);
        Assert.Equal(before, await StoredBoardAsync());
    }

    [Fact]
    public async Task Update_WithInvalidName_ShouldMapSharedServiceValidation()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var before = await StoredBoardAsync();
        var arguments = Arguments();
        arguments["name"] = "   ";

        using var payload = await CallAsync(client, token, ToolNames.BoardUpdate, arguments);

        var error = AssertError(payload, "validation_failed", 400);
        Assert.Equal("Board name is required.", error.GetProperty("validationErrors").GetProperty("name")[0].GetString());
        Assert.Equal(before, await StoredBoardAsync());
    }

    [Fact]
    public async Task Update_WithMissingBoard_ShouldReturnNotFound()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var arguments = Arguments();
        arguments["boardId"] = int.MaxValue;

        using var payload = await CallAsync(client, token, ToolNames.BoardUpdate, arguments);

        AssertError(payload, "not_found", 404);
    }

    [Fact]
    public async Task Discovery_ShouldRequireCompleteSettingsAndAdvertiseThemOnBoardGet()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);

        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/list", new { }, "board-settings-discovery", token);
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tool = McpJsonRpcClient.GetToolByName(payload, "board_update");
        var input = tool.GetProperty("inputSchema");
        Assert.Equal(["boardId", "name", "description", "settings"],
            input.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.False(input.GetProperty("additionalProperties").GetBoolean());
        var settings = input.GetProperty("properties").GetProperty("settings");
        Assert.Equal(["slickCohesionModeEnabled", "cardAttachmentThumbnailsEnabled"],
            settings.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.False(settings.GetProperty("additionalProperties").GetBoolean());
        Assert.All(settings.GetProperty("properties").EnumerateObject(), property =>
            Assert.Equal("boolean", property.Value.GetProperty("type").GetString()));
        var getOutput = McpJsonRpcClient.GetToolByName(payload, ToolNames.BoardGet).GetProperty("outputSchema");
        Assert.Contains("settings", getOutput.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.True(JsonElement.DeepEquals(settings, getOutput.GetProperty("properties").GetProperty("settings")));
    }

    private BoardOilDbContext CreateDbContext() => Factory.Services.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();

    private async Task<StoredBoard> StoredBoardAsync()
    {
        await using var db = CreateDbContext();
        return await db.Boards.AsNoTracking().Where(board => board.Id == 1)
            .Select(board => new StoredBoard(board.Name, board.Description, board.SlickCohesionModeEnabled,
                board.CardAttachmentThumbnailsEnabled, board.UpdatedAtUtc)).SingleAsync();
    }

    private static JsonObject Arguments() => new()
    {
        ["boardId"] = 1,
        ["name"] = "Updated board",
        ["description"] = "",
        ["settings"] = new JsonObject
        {
            ["slickCohesionModeEnabled"] = false,
            ["cardAttachmentThumbnailsEnabled"] = false
        }
    };

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
        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/call", new { name, arguments }, "board-settings", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await McpJsonRpcClient.ParseJsonAsync(response);
    }

    private sealed record StoredBoard(string Name, string Description, bool SlickCohesionModeEnabled,
        bool CardAttachmentThumbnailsEnabled, DateTime UpdatedAtUtc);
}
