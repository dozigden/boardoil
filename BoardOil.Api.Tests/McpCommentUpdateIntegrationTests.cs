using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Card;
using BoardOil.Ef;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpCommentUpdateIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public McpCommentUpdateIntegrationTests(DefaultApiFactoryFixture fixture)
    {
        UseSharedFactory(fixture);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SystemComments_ShouldExposeAuthorlessMarkerInRestAndMcp(bool archived)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var comment = await CreateCommentAsync(client);
        Assert.False(comment.IsSystem);
        var factory = Factory.Services.GetRequiredService<IDbContextFactory>();
        await using (var db = factory.CreateDbContext<BoardOilDbContext>())
        {
            var stored = await db.CardComments.SingleAsync(x => x.Id == comment.Id);
            stored.IsSystem = true;
            stored.AuthorUserId = null;
            await db.SaveChangesAsync();
        }
        if (archived)
        {
            var archiveResponse = await client.PostAsync($"/api/boards/1/cards/{comment.CardId}/archive", null);
            archiveResponse.EnsureSuccessStatusCode();
        }

        var mcp = await CallToolAsync(client, token, "card_get", new { boardId = 1, id = comment.CardId, archived });
        var path = archived
            ? $"/api/boards/1/cards/archived/{comment.CardId}"
            : $"/api/boards/1/cards/{comment.CardId}/comments";
        var response = await client.GetAsync(path);

        response.EnsureSuccessStatusCode();
        Assert.False(mcp.GetProperty("isError").GetBoolean());
        var rest = await response.Content.ReadFromJsonAsync<JsonElement>();
        var restComments = archived ? rest.GetProperty("data").GetProperty("comments") : rest.GetProperty("data");
        foreach (var comments in new[] { restComments, mcp.GetProperty("structuredContent").GetProperty("comments") })
        {
            var system = Assert.Single(comments.EnumerateArray());
            Assert.True(system.GetProperty("isSystem").GetBoolean());
            Assert.Equal(JsonValueKind.Null, system.GetProperty("authorUserId").ValueKind);
            Assert.Equal(JsonValueKind.Null, system.GetProperty("authorDisplayName").ValueKind);
            Assert.Equal(JsonValueKind.Null, system.GetProperty("authorImageRelativePath").ValueKind);
            Assert.Equal(comment.Text, system.GetProperty("text").GetString());
        }
    }

    [Fact]
    public async Task CommentUpdate_ShouldReturnReceiptAndExposeEditedCommentThroughCardGet()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var comment = await CreateCommentAsync(client);

        var receipt = await CallToolAsync(client, token, "card_comment_update", new { boardId = 1, cardId = comment.CardId, id = comment.Id, text = "Corrected MCP comment" });
        var read = await CallToolAsync(client, token, "card_get", new { boardId = 1, id = comment.CardId });

        Assert.False(receipt.GetProperty("isError").GetBoolean());
        var data = receipt.GetProperty("structuredContent");
        Assert.Equal(comment.Id, data.GetProperty("id").GetInt32());
        Assert.Equal("updated", data.GetProperty("outcome").GetString());
        Assert.False(read.GetProperty("isError").GetBoolean());
        var edited = Assert.Single(read.GetProperty("structuredContent").GetProperty("comments").EnumerateArray());
        Assert.Equal("Corrected MCP comment", edited.GetProperty("text").GetString());
        Assert.Equal(comment.PostedAtUtc, edited.GetProperty("postedAtUtc").GetDateTime());
        Assert.Equal(JsonValueKind.String, edited.GetProperty("editedAtUtc").ValueKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommentDelete_ShouldReturnReceiptAndRemoveCommentFromCardGet(bool repeat)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var comment = await CreateCommentAsync(client);
        if (repeat)
        {
            var firstDelete = await CallToolAsync(client, token, "card_comment_delete", new { boardId = 1, cardId = comment.CardId, id = comment.Id });
            Assert.False(firstDelete.GetProperty("isError").GetBoolean());
        }

        var receipt = await CallToolAsync(client, token, "card_comment_delete", new { boardId = 1, cardId = comment.CardId, id = comment.Id });
        var read = await CallToolAsync(client, token, "card_get", new { boardId = 1, id = comment.CardId });

        Assert.False(receipt.GetProperty("isError").GetBoolean());
        var data = receipt.GetProperty("structuredContent");
        Assert.Equal(comment.Id, data.GetProperty("id").GetInt32());
        Assert.Equal("deleted", data.GetProperty("outcome").GetString());
        Assert.False(read.GetProperty("isError").GetBoolean());
        Assert.Empty(read.GetProperty("structuredContent").GetProperty("comments").EnumerateArray());
    }

    [Theory]
    [InlineData("card_comment_update")]
    [InlineData("card_comment_delete")]
    public async Task CommentMutation_WithoutWriteScope_ShouldReturnMcpErrorAndPreserveText(string tool)
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:read"]);
        var comment = await CreateCommentAsync(client);

        object arguments = new { boardId = 1, cardId = comment.CardId, id = comment.Id };
        if (tool == "card_comment_update")
        {
            arguments = new { boardId = 1, cardId = comment.CardId, id = comment.Id, text = "Forbidden edit" };
        }
        var result = await CallToolAsync(client, token, tool, arguments);

        Assert.True(result.GetProperty("isError").GetBoolean());
        var response = await client.GetAsync($"/api/boards/1/cards/{comment.CardId}/comments");
        response.EnsureSuccessStatusCode();
        var stored = (await response.Content.ReadFromJsonAsync<ApiEnvelope<CardCommentDto[]>>())!.Data!;
        Assert.Equal(comment.Text, Assert.Single(stored).Text);
    }

    private static async Task<CardCommentDto> CreateCommentAsync(HttpClient client)
    {
        var columnResponse = await client.PostAsJsonAsync("/api/boards/1/columns", new { title = "Comment edit" });
        columnResponse.EnsureSuccessStatusCode();
        var column = await columnResponse.Content.ReadFromJsonAsync<JsonElement>();
        var cardResponse = await client.PostAsJsonAsync("/api/boards/1/cards", new { columnId = column.GetProperty("data").GetProperty("id").GetInt32(), title = "Comment edit", description = "", tagNames = Array.Empty<string>() });
        cardResponse.EnsureSuccessStatusCode();
        var card = await cardResponse.Content.ReadFromJsonAsync<JsonElement>();
        var cardId = card.GetProperty("data").GetProperty("id").GetInt32();
        var response = await client.PostAsJsonAsync($"/api/boards/1/cards/{cardId}/comments", new CreateCardCommentRequest("Original MCP comment"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<CardCommentDto>>())!.Data!;
    }

    private static async Task<JsonElement> CallToolAsync(HttpClient client, string token, string name, object arguments)
    {
        var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/call", new { name, arguments }, Guid.NewGuid().ToString("N"), token);
        response.EnsureSuccessStatusCode();
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);
        return payload.RootElement.GetProperty("result").Clone();
    }
}
