using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Card;
using BoardOil.Ef;
using BoardOil.Mcp.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpCardRestoreIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public McpCardRestoreIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    [Fact]
    public async Task CardRestore_WithWriteScope_ShouldRestoreCardAndReturnOriginalBoardScopedId()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, [MachinePatScopes.McpWrite]);
        var card = await CreateCardAsync(client, $"MCP restore {Guid.NewGuid():N}");
        using var commentResponse = await client.PostAsJsonAsync(
            $"/api/boards/1/cards/{card.Id}/comments",
            new CreateCardCommentRequest("Restored MCP comment"));
        commentResponse.EnsureSuccessStatusCode();
        var attachment = await UploadAsync(client, card.Id);
        using var archivePayload = await CallAsync(
            client,
            token,
            ToolNames.CardArchive,
            new { boardId = 1, id = card.Id });
        Assert.False(archivePayload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());

        // Act
        using var restorePayload = await CallAsync(
            client,
            token,
            ToolNames.CardRestore,
            new { boardId = 1, id = card.Id });

        // Assert
        Assert.False(restorePayload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var receipt = McpJsonRpcClient.GetStructuredContent(restorePayload);
        Assert.Equal(["id", "outcome"], receipt.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(card.Id, receipt.GetProperty("id").GetInt32());
        Assert.Equal("restored", receipt.GetProperty("outcome").GetString());

        using var livePayload = await CallAsync(
            client,
            token,
            ToolNames.CardGet,
            new { boardId = 1, id = card.Id });
        Assert.False(livePayload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var restoredCard = McpJsonRpcClient.GetStructuredContent(livePayload);
        Assert.Equal(card.Id, restoredCard.GetProperty("id").GetInt32());
        Assert.Equal(card.Title, restoredCard.GetProperty("title").GetString());
        Assert.Equal("Restored MCP comment", Assert.Single(restoredCard.GetProperty("comments").EnumerateArray())
            .GetProperty("text").GetString());
        Assert.Equal(attachment.Id, Assert.Single(restoredCard.GetProperty("attachments").EnumerateArray())
            .GetProperty("id").GetInt32());

        using var archivedPayload = await CallAsync(
            client,
            token,
            ToolNames.CardGet,
            new { boardId = 1, id = card.Id, archived = true });
        Assert.True(archivedPayload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Equal("not_found", McpJsonRpcClient.GetStructuredContent(archivedPayload).GetProperty("code").GetString());
    }

    [Fact]
    public async Task CardRestore_WithReadScope_ShouldReturnForbiddenAndLeaveCardArchived()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var card = await CreateCardAsync(client, $"MCP restore forbidden {Guid.NewGuid():N}");
        await ArchiveCardAsync(client, card.Id);
        var token = await CreateMachinePatAsync(client, [MachinePatScopes.McpRead]);

        // Act
        using var payload = await CallAsync(
            client,
            token,
            ToolNames.CardRestore,
            new { boardId = 1, id = card.Id });

        // Assert
        Assert.True(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Equal("forbidden", McpJsonRpcClient.GetStructuredContent(payload).GetProperty("code").GetString());
        using var archivedPayload = await CallAsync(
            client,
            token,
            ToolNames.CardGet,
            new { boardId = 1, id = card.Id, archived = true });
        Assert.False(archivedPayload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task CardRestore_WithNewerSnapshotVersion_ShouldReturnClearErrorAndLeaveArchiveIntact()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var card = await CreateCardAsync(client, $"MCP restore unsupported {Guid.NewGuid():N}");
        await ArchiveCardAsync(client, card.Id);
        var contextFactory = Factory.Services.GetRequiredService<IDbContextFactory>();
        await using (var dbContext = contextFactory.CreateDbContext<BoardOilDbContext>())
        {
            var archivedCard = await dbContext.ArchivedCards.SingleAsync(
                archived => archived.BoardId == 1 && archived.OriginalCardId == card.Id);
            archivedCard.SnapshotJson =
                """{"schema":"archived-card","version":999,"capturedAtUtc":"2026-09-17T00:00:00Z","payload":{}}""";
            await dbContext.SaveChangesAsync();
        }
        var token = await CreateMachinePatAsync(client, [MachinePatScopes.McpWrite]);

        // Act
        using var payload = await CallAsync(
            client,
            token,
            ToolNames.CardRestore,
            new { boardId = 1, id = card.Id });

        // Assert
        Assert.True(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var error = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal("validation_failed", error.GetProperty("code").GetString());
        Assert.Contains("cannot be restored", error.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Contains("newer than this runtime supports", error.GetProperty("message").GetString(), StringComparison.Ordinal);
        await using var assertDbContext = contextFactory.CreateDbContext<BoardOilDbContext>();
        Assert.True(await assertDbContext.ArchivedCards.AnyAsync(
            archived => archived.BoardId == 1 && archived.OriginalCardId == card.Id));
    }

    private static async Task<CardDto> CreateCardAsync(HttpClient client, string title)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(null, title, "MCP restore description", ["MCP"]));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>())!.Data!;
    }

    private static async Task ArchiveCardAsync(HttpClient client, int cardId)
    {
        using var response = await client.PostAsync($"/api/boards/1/cards/{cardId}/archive", content: null);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<CardAttachmentDto> UploadAsync(HttpClient client, int cardId)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([0, 1, 2]), "file", "mcp-restore.txt");
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
            $"card-restore-{Guid.NewGuid():N}",
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await McpJsonRpcClient.ParseJsonAsync(response);
    }
}
