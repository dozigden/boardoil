using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Card;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using BoardOil.Mcp.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpBoardDeleteIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public McpBoardDeleteIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    [Fact]
    public async Task Delete_ShouldHideBoardAndAllowItsCardsToBeRecovered()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, [MachinePatScopes.McpWrite]);
        using var created = await client.PostAsJsonAsync("/api/boards/1/cards",
            new CreateCardRequest(null, "Card before deletion", "", []));
        created.EnsureSuccessStatusCode();
        var card = (await created.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>())!.Data!;

        using var deletion = await CallAsync(client, token, ToolNames.BoardDelete, new { boardId = 1 });

        AssertDeleted(deletion, 1);
        Assert.NotNull(await GetDeletionTimeAsync());
        using var list = await CallAsync(client, token, ToolNames.BoardList, new { });
        Assert.False(list.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Empty(McpJsonRpcClient.GetStructuredContent(list).GetProperty("boards").EnumerateArray());
        using var get = await CallAsync(client, token, ToolNames.BoardGet, new { id = 1 });
        AssertError(get, "not_found", 404);

        using var restore = await client.PostAsync("/api/system/boards/1/restore", null);
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        using var recovered = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id = card.Id });
        Assert.False(recovered.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Equal(card.Title, McpJsonRpcClient.GetStructuredContent(recovered).GetProperty("title").GetString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delete_WhenAlreadyDeletedOrMissing_ShouldSucceedWithoutChanges(bool alreadyDeleted)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, [MachinePatScopes.McpWrite]);
        var boardId = alreadyDeleted ? 1 : int.MaxValue;
        DateTime? deletedAtUtc = null;
        if (alreadyDeleted)
        {
            deletedAtUtc = new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
            await using var db = CreateDbContext();
            var board = await db.Boards.SingleAsync();
            board.DeletedAtUtc = deletedAtUtc;
            await db.SaveChangesAsync();
        }

        using var payload = await CallAsync(client, token, ToolNames.BoardDelete, new { boardId });

        AssertDeleted(payload, boardId);
        Assert.Equal(deletedAtUtc, await GetDeletionTimeAsync());
    }

    [Theory]
    [InlineData("read scope")]
    [InlineData("contributor")]
    [InlineData("non-member")]
    public async Task Delete_WithoutWriteScopeOrOwnership_ShouldLeaveBoardActive(string restriction)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var scope = restriction == "read scope" ? MachinePatScopes.McpRead : MachinePatScopes.McpWrite;
        var token = await CreateMachinePatAsync(client, [scope]);
        if (scope == MachinePatScopes.McpWrite)
        {
            await using var db = CreateDbContext();
            var membership = await db.BoardMembers.SingleAsync();
            if (restriction == "contributor")
            {
                membership.Role = BoardMemberRole.Contributor;
            }
            else
            {
                db.BoardMembers.Remove(membership);
            }
            await db.SaveChangesAsync();
        }

        using var payload = await CallAsync(client, token, ToolNames.BoardDelete, new { boardId = 1 });

        AssertError(payload, "forbidden", 403);
        Assert.Null(await GetDeletionTimeAsync());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"boardId\":null}")]
    [InlineData("{\"boardId\":0}")]
    public async Task Delete_WithInvalidBoardId_ShouldReturnFieldError(string argumentsJson)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, [MachinePatScopes.McpWrite]);
        using var arguments = JsonDocument.Parse(argumentsJson);

        using var payload = await CallAsync(client, token, ToolNames.BoardDelete, arguments.RootElement);

        var error = AssertError(payload, "validation_failed", 400);
        Assert.True(error.GetProperty("validationErrors").TryGetProperty("boardId", out _));
        Assert.Null(await GetDeletionTimeAsync());
    }

    private BoardOilDbContext CreateDbContext() =>
        Factory.Services.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();

    private async Task<DateTime?> GetDeletionTimeAsync()
    {
        await using var db = CreateDbContext();
        return await db.Boards.Where(board => board.Id == 1).Select(board => board.DeletedAtUtc).SingleAsync();
    }

    private static void AssertDeleted(JsonDocument payload, int boardId)
    {
        Assert.False(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var receipt = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal(boardId, receipt.GetProperty("id").GetInt32());
        Assert.Equal("deleted", receipt.GetProperty("outcome").GetString());
    }

    private static JsonElement AssertError(JsonDocument payload, string code, int statusCode)
    {
        Assert.True(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var error = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.Equal(statusCode, error.GetProperty("statusCode").GetInt32());
        return error;
    }

    private static async Task<JsonDocument> CallAsync(HttpClient client, string token, string name, object arguments)
    {
        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/call", new { name, arguments }, "board-delete", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await McpJsonRpcClient.ParseJsonAsync(response);
    }
}
