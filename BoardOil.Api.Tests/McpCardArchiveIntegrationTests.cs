using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Card;
using BoardOil.Mcp.Contracts;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpCardArchiveIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public McpCardArchiveIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    [Fact]
    public async Task CardArchive_WithWriteScope_ShouldArchiveCardAndReturnBoardScopedReceipt()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, [MachinePatScopes.McpWrite]);
        var card = await CreateCardAsync(client, $"MCP archive {Guid.NewGuid():N}");
        using var commentResponse = await client.PostAsJsonAsync(
            $"/api/boards/1/cards/{card.Id}/comments",
            new CreateCardCommentRequest("Preserved archive comment"));
        commentResponse.EnsureSuccessStatusCode();
        var attachment = await UploadAsync(client, card.Id);

        // Act
        using var archivePayload = await CallAsync(
            client,
            token,
            ToolNames.CardArchive,
            new { boardId = 1, id = card.Id });

        // Assert
        Assert.False(archivePayload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var receipt = McpJsonRpcClient.GetStructuredContent(archivePayload);
        Assert.Equal(["id", "outcome"], receipt.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(card.Id, receipt.GetProperty("id").GetInt32());
        Assert.Equal("archived", receipt.GetProperty("outcome").GetString());

        using var livePayload = await CallAsync(
            client,
            token,
            ToolNames.CardGet,
            new { boardId = 1, id = card.Id });
        Assert.True(livePayload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Equal("not_found", McpJsonRpcClient.GetStructuredContent(livePayload).GetProperty("code").GetString());

        using var archivedPayload = await CallAsync(
            client,
            token,
            ToolNames.CardGet,
            new { boardId = 1, id = card.Id, archived = true });
        Assert.False(archivedPayload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var archivedCard = McpJsonRpcClient.GetStructuredContent(archivedPayload);
        Assert.Equal(card.Id, archivedCard.GetProperty("id").GetInt32());
        Assert.Equal(card.Title, archivedCard.GetProperty("title").GetString());
        Assert.Equal("MCP archive description", archivedCard.GetProperty("description").GetString());
        Assert.Equal(["MCP"], archivedCard.GetProperty("tagNames").EnumerateArray().Select(tag => tag.GetString()).ToArray());
        Assert.Equal("Preserved archive comment", Assert.Single(archivedCard.GetProperty("comments").EnumerateArray())
            .GetProperty("text").GetString());
        Assert.Equal(attachment.Id, Assert.Single(archivedCard.GetProperty("attachments").EnumerateArray())
            .GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task CardArchive_WithReadScope_ShouldReturnForbiddenAndLeaveCardLive()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, [MachinePatScopes.McpRead]);
        var card = await CreateCardAsync(client, $"MCP archive forbidden {Guid.NewGuid():N}");

        // Act
        using var payload = await CallAsync(
            client,
            token,
            ToolNames.CardArchive,
            new { boardId = 1, id = card.Id });

        // Assert
        Assert.True(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Equal("forbidden", McpJsonRpcClient.GetStructuredContent(payload).GetProperty("code").GetString());
        using var livePayload = await CallAsync(
            client,
            token,
            ToolNames.CardGet,
            new { boardId = 1, id = card.Id });
        Assert.False(livePayload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    private static async Task<CardDto> CreateCardAsync(HttpClient client, string title)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(null, title, "MCP archive description", ["MCP"]));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>())!.Data!;
    }

    private static async Task<CardAttachmentDto> UploadAsync(HttpClient client, int cardId)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([0, 1, 2]), "file", "mcp-archive.txt");
        using var response = await client.PostAsync($"/api/boards/1/cards/{cardId}/attachments", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<CardAttachmentDto>>())!.Data!;
    }

    private static async Task<JsonDocument> CallAsync(
        HttpClient client,
        string token,
        string toolName,
        object arguments)
    {
        using var response = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new { name = toolName, arguments },
            $"card-archive-{Guid.NewGuid():N}",
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await McpJsonRpcClient.ParseJsonAsync(response);
    }

    private sealed record ApiEnvelope<T>(bool Success, T? Data);
}
