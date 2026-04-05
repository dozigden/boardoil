using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpToolDiscoveryIntegrationTests : McpIntegrationTestBase
{
    [Fact]
    public async Task WellKnownMcp_ShouldReturnAuthAndEndpointMetadata()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/.well-known/mcp");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("BoardOil MCP", payload.RootElement.GetProperty("name").GetString());
        Assert.Equal("mcp-http", payload.RootElement.GetProperty("protocol").GetString());
        Assert.Equal("/mcp", payload.RootElement.GetProperty("endpoint").GetString());
        Assert.Equal("Bearer", payload.RootElement.GetProperty("auth").GetProperty("scheme").GetString());
        Assert.Equal("personal_access_token", payload.RootElement.GetProperty("setup").GetProperty("preferredAuth").GetString());
        Assert.Equal("/access-tokens", payload.RootElement.GetProperty("setup").GetProperty("patManagementUi").GetString());
        Assert.Equal("/mcp", payload.RootElement
            .GetProperty("setup")
            .GetProperty("examples")
            .GetProperty("genericMcpConfig")
            .GetProperty("url")
            .GetString());
        Assert.Equal("POST", payload.RootElement.GetProperty("examples").GetProperty("toolsListRequest").GetProperty("method").GetString());
    }

    [Fact]
    public async Task WellKnownMcp_WithConfiguredPublicBaseUrl_ShouldReturnAbsoluteMetadataUrls()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var putResponse = await client.PutAsJsonAsync("/api/configuration", new UpdateConfigurationRequest("https://boardoil.example.com/base"));
        putResponse.EnsureSuccessStatusCode();

        // Act
        var response = await client.GetAsync("/.well-known/mcp");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("https://boardoil.example.com/base/mcp", payload.RootElement.GetProperty("endpoint").GetString());
        Assert.Equal(
            "personal_access_token",
            payload.RootElement.GetProperty("auth").GetProperty("tokenType").GetString());
        Assert.Equal(
            "bo_pat_",
            payload.RootElement.GetProperty("auth").GetProperty("tokenPrefix").GetString());
        Assert.Equal(
            "https://boardoil.example.com/base/access-tokens",
            payload.RootElement.GetProperty("setup").GetProperty("patManagementUi").GetString());
        Assert.Equal(
            "https://boardoil.example.com/base/access-tokens",
            payload.RootElement.GetProperty("auth").GetProperty("patManagementUi").GetString());
    }

    [Fact]
    public async Task WellKnownMcp_ShouldExposeTopLevelExamples()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/.well-known/mcp");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("http", payload.RootElement
            .GetProperty("examples")
            .GetProperty("genericMcpConfig")
            .GetProperty("transport")
            .GetString());
        Assert.Equal("POST", payload.RootElement
            .GetProperty("examples")
            .GetProperty("toolsListRequest")
            .GetProperty("method")
            .GetString());
    }

    [Fact]
    public async Task ToolsList_ShouldAdvertiseDeterministicToolsAndCanonicalIdFieldsInSchemas()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);

        // Act
        var toolsListResponse = await McpJsonRpcClient.SendRequestAsync(client, "tools/list", new { }, "tools-list-schemas", patToken);
        Assert.Equal(HttpStatusCode.OK, toolsListResponse.StatusCode);
        using var toolsListPayload = await McpJsonRpcClient.ParseJsonAsync(toolsListResponse);

        // Assert
        var toolNames = toolsListPayload.RootElement
            .GetProperty("result")
            .GetProperty("tools")
            .EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString())
            .ToArray();
        Assert.DoesNotContain("card.move_by_column_name", toolNames);

        var boardGetTool = McpJsonRpcClient.GetToolByName(toolsListPayload, "board.get");
        var boardGetProperties = boardGetTool.GetProperty("inputSchema").GetProperty("properties");
        Assert.True(boardGetProperties.TryGetProperty("id", out _));
        Assert.False(boardGetProperties.TryGetProperty("boardId", out _));

        var cardMoveTool = McpJsonRpcClient.GetToolByName(toolsListPayload, "card.move");
        var cardMoveProperties = cardMoveTool.GetProperty("inputSchema").GetProperty("properties");
        Assert.True(cardMoveProperties.TryGetProperty("id", out _));
        Assert.True(cardMoveProperties.TryGetProperty("columnId", out _));
        Assert.True(cardMoveProperties.TryGetProperty("afterId", out _));
        Assert.False(cardMoveProperties.TryGetProperty("cardId", out _));
        Assert.False(cardMoveProperties.TryGetProperty("boardColumnId", out _));
        Assert.False(cardMoveProperties.TryGetProperty("positionAfterCardId", out _));

        var cardCreateTool = McpJsonRpcClient.GetToolByName(toolsListPayload, "card.create");
        var cardCreateProperties = cardCreateTool.GetProperty("inputSchema").GetProperty("properties");
        Assert.True(cardCreateProperties.TryGetProperty("cardTypeId", out _));
        var cardCreateRequired = cardCreateTool.GetProperty("inputSchema").GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.DoesNotContain("cardTypeId", cardCreateRequired);

        var cardUpdateTool = McpJsonRpcClient.GetToolByName(toolsListPayload, "card.update");
        var cardUpdateProperties = cardUpdateTool.GetProperty("inputSchema").GetProperty("properties");
        Assert.True(cardUpdateProperties.TryGetProperty("cardTypeId", out _));
        var cardUpdateRequired = cardUpdateTool.GetProperty("inputSchema").GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("cardTypeId", cardUpdateRequired);
    }

    private sealed record UpdateConfigurationRequest(string? McpPublicBaseUrl);
}
