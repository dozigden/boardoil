using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Tag;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpTagCrudIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public McpTagCrudIntegrationTests(DefaultApiFactoryFixture fixture)
    {
        UseSharedFactory(fixture);
    }

    [Fact]
    public async Task TagCreate_WithCompleteDefinition_ShouldReturnCanonicalCreatedTag()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client, ["mcp:write"]);

        // Act
        var response = await CallTagCreateAsync(
            client,
            patToken,
            new
            {
                boardId = 1,
                name = "Cats",
                emoji = "🐈‍⬛",
                style = new
                {
                    styleName = "solid",
                    backgroundColor = "#99c1f1",
                    textColorMode = "custom",
                    textColor = "#ffffff",
                    borderMode = "none"
                }
            },
            "tag-create-complete");
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var output = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal("created", output.GetProperty("outcome").GetString());
        var tag = output.GetProperty("tag");
        Assert.True(tag.GetProperty("id").GetInt32() > 0);
        Assert.Equal("Cats", tag.GetProperty("name").GetString());
        Assert.Equal("🐈‍⬛", tag.GetProperty("emoji").GetString());
        var style = tag.GetProperty("style");
        Assert.Equal("solid", style.GetProperty("styleName").GetString());
        Assert.Equal("#99C1F1", style.GetProperty("backgroundColor").GetString());
        Assert.Equal("#FFFFFF", style.GetProperty("textColor").GetString());
        Assert.Equal("none", style.GetProperty("borderMode").GetString());
    }

    [Fact]
    public async Task TagCreate_WhenNameAlreadyExists_ShouldReturnExistingTagWithoutMutation()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var existingTag = await CreateTagAsync(client, "Existing", "🏷️");
        var patToken = await CreateMachinePatAsync(client, ["mcp:write"]);

        // Act
        var response = await CallTagCreateAsync(
            client,
            patToken,
            new
            {
                boardId = 1,
                name = "EXISTING",
                emoji = "🚀",
                style = new { styleName = "auto" }
            },
            "tag-create-existing");
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var output = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal("existing", output.GetProperty("outcome").GetString());
        var tag = output.GetProperty("tag");
        Assert.Equal(existingTag.Id, tag.GetProperty("id").GetInt32());
        Assert.Equal("Existing", tag.GetProperty("name").GetString());
        Assert.Equal("🏷️", tag.GetProperty("emoji").GetString());
        Assert.Equal(existingTag.StyleName, tag.GetProperty("style").GetProperty("styleName").GetString());
    }

    [Fact]
    public async Task TagDelete_WithExistingTag_ShouldDeleteDefinition()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var existingTag = await CreateTagAsync(client, "Delete me", null);
        var patToken = await CreateMachinePatAsync(client, ["mcp:write"]);

        // Act
        var response = await CallTagDeleteAsync(client, patToken, existingTag.Id, "tag-delete-existing");
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var output = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal("deleted", output.GetProperty("outcome").GetString());
        var tagsEnvelope = await client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<TagDto>>>("/api/boards/1/tags");
        Assert.NotNull(tagsEnvelope?.Data);
        Assert.DoesNotContain(tagsEnvelope.Data, tag => tag.Id == existingTag.Id);
    }

    [Fact]
    public async Task TagDelete_WhenTagIsMissing_ShouldRemainIdempotent()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client, ["mcp:write"]);

        // Act
        var response = await CallTagDeleteAsync(client, patToken, int.MaxValue, "tag-delete-missing");
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Equal("deleted", McpJsonRpcClient.GetStructuredContent(payload).GetProperty("outcome").GetString());
    }

    private static Task<HttpResponseMessage> CallTagCreateAsync(
        HttpClient client,
        string patToken,
        object arguments,
        string requestId) =>
        McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new { name = "tag_create", arguments },
            requestId,
            patToken);

    private static Task<HttpResponseMessage> CallTagDeleteAsync(
        HttpClient client,
        string patToken,
        int tagId,
        string requestId) =>
        McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "tag_delete",
                arguments = new { boardId = 1, id = tagId }
            },
            requestId,
            patToken);

    private static async Task<TagDto> CreateTagAsync(
        HttpClient client,
        string name,
        string? emoji)
    {
        var response = await client.PostAsJsonAsync(
            "/api/boards/1/tags",
            new CreateTagRequest(name, emoji));
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<TagDto>>();
        Assert.NotNull(payload?.Data);
        return payload.Data;
    }
}
