using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Card;
using BoardOil.Mcp.Contracts;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpCardBulkIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public McpCardBulkIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    [Theory]
    [InlineData(ToolNames.CardArchive, "archivedCount", true)]
    [InlineData(ToolNames.CardDelete, "deletedCount", false)]
    public async Task BulkMutation_WithWriteScope_ShouldReturnSummaryAndRemoveLiveCards(
        string toolName, string countProperty, bool archive)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, [MachinePatScopes.McpWrite]);
        var first = await CreateCardAsync(client);
        var second = await CreateCardAsync(client);
        int[] cardIds = [first.Id, second.Id];
        var requestedCount = 2;
        if (!archive)
        {
            cardIds = [first.Id, second.Id, first.Id, int.MaxValue];
            requestedCount = 3;
        }

        // Act
        using var payload = await CallAsync(client, token, toolName, new { boardId = 1, cardIds });

        // Assert
        Assert.False(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var summary = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal(["boardId", "requestedCount", countProperty], summary.EnumerateObject().Select(property => property.Name));
        Assert.Equal(1, summary.GetProperty("boardId").GetInt32());
        Assert.Equal(requestedCount, summary.GetProperty("requestedCount").GetInt32());
        Assert.Equal(2, summary.GetProperty(countProperty).GetInt32());
        foreach (var card in new[] { first, second })
        {
            using var live = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id = card.Id });
            Assert.Equal("not_found", McpJsonRpcClient.GetStructuredContent(live).GetProperty("code").GetString());
            using var archived = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id = card.Id, archived = true });
            Assert.Equal(!archive, archived.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
            if (archive)
            {
                Assert.Equal(card.Title, McpJsonRpcClient.GetStructuredContent(archived).GetProperty("title").GetString());
            }
        }
    }

    [Theory]
    [InlineData(ToolNames.CardArchive)]
    [InlineData(ToolNames.CardDelete)]
    public async Task BulkMutation_WithReadScope_ShouldReturnForbiddenAndLeaveCardLive(string toolName)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, [MachinePatScopes.McpRead]);
        var card = await CreateCardAsync(client);

        // Act
        using var payload = await CallAsync(client, token, toolName, new { boardId = 1, cardIds = new[] { card.Id } });

        // Assert
        Assert.True(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Equal("forbidden", McpJsonRpcClient.GetStructuredContent(payload).GetProperty("code").GetString());
        using var live = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id = card.Id });
        Assert.False(live.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task BulkArchive_WithDuplicateIds_ShouldMapServiceValidationAndLeaveCardLive()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, [MachinePatScopes.McpWrite]);
        var card = await CreateCardAsync(client);

        // Act
        using var payload = await CallAsync(client, token, ToolNames.CardArchive,
            new { boardId = 1, cardIds = new[] { card.Id, card.Id } });

        // Assert
        Assert.True(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Equal("validation_failed", McpJsonRpcClient.GetStructuredContent(payload).GetProperty("code").GetString());
        using var live = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id = card.Id });
        Assert.False(live.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [Theory]
    [InlineData(ToolNames.CardArchive)]
    [InlineData(ToolNames.CardDelete)]
    public async Task Mutation_WithOldSingleIdArgument_ShouldRejectItAndLeaveCardLive(string toolName)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, [MachinePatScopes.McpWrite]);
        var card = await CreateCardAsync(client);

        // Act
        using var payload = await CallAsync(client, token, toolName, new { boardId = 1, id = card.Id });

        // Assert
        Assert.True(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var error = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal("validation_failed", error.GetProperty("code").GetString());
        Assert.Contains("Unknown tool arguments: id", error.GetProperty("message").GetString(), StringComparison.Ordinal);
        using var live = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id = card.Id });
        Assert.False(live.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_WithoutCardIds_ShouldReturnValidationError(bool includeNull)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, [MachinePatScopes.McpWrite]);
        var arguments = new Dictionary<string, object?> { ["boardId"] = 1 };
        if (includeNull)
        {
            arguments["cardIds"] = null;
        }

        // Act
        using var payload = await CallAsync(client, token, ToolNames.CardDelete, arguments);

        // Assert
        Assert.True(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var error = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal("validation_failed", error.GetProperty("code").GetString());
        Assert.NotEmpty(error.GetProperty("validationErrors").GetProperty("cardIds").EnumerateArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Delete_WithNonPositiveId_ShouldRejectWholeRequestAndLeaveCardLive(int invalidId)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, [MachinePatScopes.McpWrite]);
        var card = await CreateCardAsync(client);

        // Act
        using var payload = await CallAsync(client, token, ToolNames.CardDelete,
            new { boardId = 1, cardIds = new[] { card.Id, invalidId } });

        // Assert
        Assert.True(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var error = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal("validation_failed", error.GetProperty("code").GetString());
        Assert.NotEmpty(error.GetProperty("validationErrors").GetProperty("cardIds").EnumerateArray());
        using var live = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id = card.Id });
        Assert.False(live.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task Delete_WithEmptyCardIds_ShouldReturnZeroCountsAndLeaveCardLive()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, [MachinePatScopes.McpWrite]);
        var card = await CreateCardAsync(client);

        // Act
        using var payload = await CallAsync(client, token, ToolNames.CardDelete,
            new { boardId = 1, cardIds = Array.Empty<int>() });

        // Assert
        Assert.False(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var summary = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal(1, summary.GetProperty("boardId").GetInt32());
        Assert.Equal(0, summary.GetProperty("requestedCount").GetInt32());
        Assert.Equal(0, summary.GetProperty("deletedCount").GetInt32());
        using var live = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id = card.Id });
        Assert.False(live.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    private static async Task<CardDto> CreateCardAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/boards/1/cards",
            new CreateCardRequest(null, $"MCP bulk {Guid.NewGuid():N}", "Bulk test description", []));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>())!.Data!;
    }

    private static async Task<JsonDocument> CallAsync(HttpClient client, string token, string toolName, object arguments)
    {
        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/call",
            new { name = toolName, arguments }, $"card-bulk-{Guid.NewGuid():N}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await McpJsonRpcClient.ParseJsonAsync(response);
    }
}
