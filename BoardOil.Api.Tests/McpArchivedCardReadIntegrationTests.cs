using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Card;
using BoardOil.Mcp.Contracts;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpArchivedCardReadIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public McpArchivedCardReadIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    [Theory]
    [InlineData(MachinePatScopes.McpRead)]
    [InlineData(MachinePatScopes.McpWrite)]
    public async Task CardSearch_WithArchivedSelector_ShouldSearchTitleAndTagsWithEffectiveReadScope(string scope)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, [scope]);
        var marker = $"Archive{Guid.NewGuid().ToString("N")[..8]}";
        var card = await CreateCardAsync(client, $"Archived search target {Guid.NewGuid():N}", [marker]);
        await ArchiveCardAsync(client, card.Id);

        // Act
        using var payload = await CallAsync(client, token, ToolNames.CardSearch,
            new { boardId = 1, query = marker.ToLowerInvariant(), archived = true });

        // Assert
        Assert.False(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var output = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal(["cards", "totalCount", "offset", "limit"],
            output.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(1, output.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, output.GetProperty("offset").GetInt32());
        Assert.Equal(20, output.GetProperty("limit").GetInt32());
        var resultCard = Assert.Single(output.GetProperty("cards").EnumerateArray());
        Assert.Equal(["id", "title", "tagNames", "archivedAtUtc"],
            resultCard.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(card.Id, resultCard.GetProperty("id").GetInt32());
        Assert.Equal(card.Title, resultCard.GetProperty("title").GetString());
        Assert.Equal([marker], resultCard.GetProperty("tagNames").EnumerateArray().Select(tag => Assert.IsType<string>(tag.GetString())).ToArray());
        Assert.NotEqual(default, resultCard.GetProperty("archivedAtUtc").GetDateTime());
    }

    [Theory]
    [InlineData(MachinePatScopes.McpRead)]
    [InlineData(MachinePatScopes.McpWrite)]
    public async Task CardGet_WithArchivedSelector_ShouldReturnTypedDetailWithoutLiveCommentIds(string scope)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, [scope]);
        var card = await CreateCardAsync(client, $"Archived detail {Guid.NewGuid():N}", ["Archive detail"]);
        using var commentResponse = await client.PostAsJsonAsync(
            $"/api/boards/1/cards/{card.Id}/comments",
            new CreateCardCommentRequest("Archived MCP comment"));
        commentResponse.EnsureSuccessStatusCode();
        var attachment = await UploadAsync(client, card.Id, "archived-mcp.txt");
        await ArchiveCardAsync(client, card.Id);

        // Act
        using var payload = await CallAsync(client, token, ToolNames.CardGet,
            new { boardId = 1, id = card.Id, archived = true });

        // Assert
        Assert.False(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var output = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal(card.Id, output.GetProperty("id").GetInt32());
        Assert.Equal(card.Title, output.GetProperty("title").GetString());
        Assert.Equal("Archived card description", output.GetProperty("description").GetString());
        Assert.NotEqual(default, output.GetProperty("archivedAtUtc").GetDateTime());
        var returnedAttachment = Assert.Single(output.GetProperty("attachments").EnumerateArray());
        Assert.Equal(attachment.Id, returnedAttachment.GetProperty("id").GetInt32());
        Assert.Equal("archived-mcp.txt", returnedAttachment.GetProperty("originalFileName").GetString());
        Assert.Equal(5, returnedAttachment.GetProperty("byteLength").GetInt64());
        Assert.False(output.TryGetProperty("snapshotJson", out _));
        var comment = Assert.Single(output.GetProperty("comments").EnumerateArray());
        Assert.Equal(
            ["text", "postedAtUtc", "authorUserId", "authorDisplayName", "authorImageRelativePath"],
            comment.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal("Archived MCP comment", comment.GetProperty("text").GetString());
        Assert.False(comment.TryGetProperty("id", out _));
        Assert.False(comment.TryGetProperty("cardId", out _));
    }

    private static async Task<CardDto> CreateCardAsync(HttpClient client, string title, string[] tags)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(null, title, "Archived card description", tags));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>())!.Data!;
    }

    private static async Task ArchiveCardAsync(HttpClient client, int cardId)
    {
        using var response = await client.PostAsync($"/api/boards/1/cards/{cardId}/archive", content: null);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<CardAttachmentDto> UploadAsync(HttpClient client, int cardId, string fileName)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([0, 1, 2, 3, 4]), "file", fileName);
        using var response = await client.PostAsync($"/api/boards/1/cards/{cardId}/attachments", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<CardAttachmentDto>>())!.Data!;
    }

    private static async Task<JsonDocument> CallAsync(HttpClient client, string token, string toolName, object arguments)
    {
        using var response = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new { name = toolName, arguments },
            $"archived-read-{toolName}",
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await McpJsonRpcClient.ParseJsonAsync(response);
    }
}
