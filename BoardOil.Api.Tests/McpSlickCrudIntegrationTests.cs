using System.Net;
using System.Net.Http.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Slick;
using BoardOil.Mcp.Contracts;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpSlickCrudIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public McpSlickCrudIntegrationTests(DefaultApiFactoryFixture fixture)
    {
        UseSharedFactory(fixture);
    }

    [Fact]
    public async Task SlickCreate_WithCompleteDefinition_ShouldReturnCanonicalCreatedSlick()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client, ["mcp:write"]);

        // Act
        var response = await CallSlickCreateAsync(
            client,
            patToken,
            new
            {
                boardId = 1,
                name = "MCP Release Train",
                style = new
                {
                    styleName = "solid",
                    backgroundColor = "#99c1f1",
                    textColorMode = "custom",
                    textColor = "#ffffff",
                    borderMode = "none"
                }
            },
            "slick-create-complete");
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var output = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal("created", output.GetProperty("outcome").GetString());
        var slick = output.GetProperty("slick");
        Assert.True(slick.GetProperty("id").GetInt32() > 0);
        Assert.Equal("MCP Release Train", slick.GetProperty("name").GetString());
        var style = slick.GetProperty("style");
        Assert.Equal("solid", style.GetProperty("styleName").GetString());
        Assert.Equal("#99C1F1", style.GetProperty("backgroundColor").GetString());
        Assert.Equal("#FFFFFF", style.GetProperty("textColor").GetString());
        Assert.Equal("none", style.GetProperty("borderMode").GetString());
    }

    [Fact]
    public async Task SlickCreate_WhenNameAlreadyExists_ShouldReturnExistingWithoutMutation()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var existingSlick = await CreateSlickAsync(client, "Existing MCP Slick");
        var patToken = await CreateMachinePatAsync(client, ["mcp:write"]);

        // Act
        var response = await CallSlickCreateAsync(
            client,
            patToken,
            new
            {
                boardId = 1,
                name = "EXISTING MCP SLICK",
                style = new
                {
                    styleName = "solid",
                    backgroundColor = "#224466",
                    textColorMode = "auto",
                    borderMode = "auto"
                }
            },
            "slick-create-existing");
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var output = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal("existing", output.GetProperty("outcome").GetString());
        var slick = output.GetProperty("slick");
        Assert.Equal(existingSlick.Id, slick.GetProperty("id").GetInt32());
        Assert.Equal("Existing MCP Slick", slick.GetProperty("name").GetString());
        Assert.Equal("presets", slick.GetProperty("style").GetProperty("styleName").GetString());
    }

    [Fact]
    public async Task SlickCreate_WithInvalidStyle_ShouldReturnFieldAddressableErrors()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client, ["mcp:write"]);

        // Act
        var response = await CallSlickCreateAsync(
            client,
            patToken,
            new
            {
                boardId = 1,
                name = "Invalid MCP Slick",
                style = new
                {
                    styleName = "solid",
                    textColorMode = "auto",
                    borderMode = "auto"
                }
            },
            "slick-create-invalid-style");
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var output = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal("validation_failed", output.GetProperty("code").GetString());
        Assert.True(output.GetProperty("validationErrors").TryGetProperty("style.backgroundColor", out _));
    }

    [Fact]
    public async Task SlickCreate_WithTagOnlyStyleKind_ShouldReturnStyleNameError()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client, ["mcp:write"]);

        // Act
        var response = await CallSlickCreateAsync(
            client,
            patToken,
            new
            {
                boardId = 1,
                name = "Unsupported MCP Slick",
                style = new { styleName = "auto" }
            },
            "slick-create-tag-only-style");
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var output = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal("validation_failed", output.GetProperty("code").GetString());
        Assert.True(output.GetProperty("validationErrors").TryGetProperty("style.styleName", out _));
    }

    [Fact]
    public async Task SlickUpdate_WithNameAndStyle_ShouldReturnCanonicalUpdatedSlick()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var existingSlick = await CreateSlickAsync(client, "Update MCP Slick");
        var patToken = await CreateMachinePatAsync(client, ["mcp:write"]);

        // Act
        var response = await CallSlickUpdateAsync(
            client,
            patToken,
            new
            {
                boardId = 1,
                id = existingSlick.Id,
                name = "Updated MCP Slick",
                style = new { styleName = "presets", presetIndex = 7 }
            },
            "slick-update-complete");
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var output = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal("updated", output.GetProperty("outcome").GetString());
        var slick = output.GetProperty("slick");
        Assert.Equal(existingSlick.Id, slick.GetProperty("id").GetInt32());
        Assert.Equal("Updated MCP Slick", slick.GetProperty("name").GetString());
        Assert.Equal(7, slick.GetProperty("style").GetProperty("presetIndex").GetInt32());
    }

    [Fact]
    public async Task SlickDelete_WhenSlickExists_ShouldDeleteDefinition()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var existingSlick = await CreateSlickAsync(client, "Delete MCP Slick");
        var patToken = await CreateMachinePatAsync(client, ["mcp:write"]);

        // Act
        var response = await CallSlickDeleteAsync(
            client,
            patToken,
            existingSlick.Id,
            "slick-delete-existing");
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("deleted", McpJsonRpcClient.GetStructuredContent(payload).GetProperty("outcome").GetString());
        var slicksEnvelope = await client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<SlickDto>>>("/api/boards/1/slicks");
        Assert.NotNull(slicksEnvelope?.Data);
        Assert.DoesNotContain(slicksEnvelope.Data, slick => slick.Id == existingSlick.Id);
    }

    [Fact]
    public async Task SlickDelete_WhenSlickIsMissing_ShouldRemainIdempotent()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client, ["mcp:write"]);

        // Act
        var response = await CallSlickDeleteAsync(
            client,
            patToken,
            int.MaxValue,
            "slick-delete-missing");
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Equal("deleted", McpJsonRpcClient.GetStructuredContent(payload).GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task SlickCreate_WithReadOnlyPat_ShouldReturnForbidden()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client, ["mcp:read"]);

        // Act
        var response = await CallSlickCreateAsync(
            client,
            patToken,
            new
            {
                boardId = 1,
                name = "Forbidden MCP Slick",
                style = new { styleName = "presets", presetIndex = 4 }
            },
            "slick-create-read-only");
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Equal("forbidden", McpJsonRpcClient.GetStructuredContent(payload).GetProperty("code").GetString());
    }

    private static Task<HttpResponseMessage> CallSlickCreateAsync(
        HttpClient client,
        string patToken,
        object arguments,
        string requestId) =>
        McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new { name = ToolNames.SlickCreate, arguments },
            requestId,
            patToken);

    private static Task<HttpResponseMessage> CallSlickUpdateAsync(
        HttpClient client,
        string patToken,
        object arguments,
        string requestId) =>
        McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new { name = ToolNames.SlickUpdate, arguments },
            requestId,
            patToken);

    private static Task<HttpResponseMessage> CallSlickDeleteAsync(
        HttpClient client,
        string patToken,
        int slickId,
        string requestId) =>
        McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = ToolNames.SlickDelete,
                arguments = new { boardId = 1, id = slickId }
            },
            requestId,
            patToken);

    private static async Task<SlickDto> CreateSlickAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(
            "/api/boards/1/slicks",
            new CreateSlickRequest(name, "presets", """{"presetIndex":2}"""));
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<SlickDto>>();
        Assert.NotNull(payload?.Data);
        return payload.Data;
    }
}
