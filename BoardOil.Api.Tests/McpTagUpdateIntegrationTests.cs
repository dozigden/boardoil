using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Tag;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpTagUpdateIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public McpTagUpdateIntegrationTests(DefaultApiFactoryFixture fixture)
    {
        UseSharedFactory(fixture);
    }

    public static TheoryData<string, object> SupportedStyles => new()
    {
        { "auto", new { styleName = "auto" } },
        { "presets", new { styleName = "presets", presetIndex = 7 } },
        {
            "solid",
            new
            {
                styleName = "solid",
                backgroundColor = "#aabbcc",
                textColorMode = "custom",
                textColor = "#112233",
                borderMode = "custom",
                borderColor = "#445566"
            }
        },
        {
            "gradient",
            new
            {
                styleName = "gradient",
                leftColor = "#123456",
                rightColor = "#abcdef",
                textColorMode = "auto",
                textColor = (string?)null,
                borderMode = "none",
                borderColor = (string?)null
            }
        }
    };

    [Theory]
    [MemberData(nameof(SupportedStyles))]
    public async Task TagUpdate_WithSupportedStyle_ShouldReturnStructuredCanonicalStyle(
        string expectedStyleName,
        object style)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);
        var tagName = $"Style {expectedStyleName}";
        var originalTag = await CreateTagAsync(client, tagName, "🏷️");

        // Act
        var response = await CallTagUpdateAsync(
            client,
            patToken,
            new
            {
                boardId = 1,
                currentTagName = tagName.ToUpperInvariant(),
                style
            },
            $"tag-update-{expectedStyleName}");
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var updatedTag = McpJsonRpcClient.GetStructuredContent(payload).GetProperty("tag");
        Assert.Equal(originalTag.Id, updatedTag.GetProperty("id").GetInt32());
        Assert.Equal(tagName, updatedTag.GetProperty("name").GetString());
        Assert.Equal("🏷️", updatedTag.GetProperty("emoji").GetString());
        Assert.False(updatedTag.TryGetProperty("stylePropertiesJson", out _));
        var updatedStyle = updatedTag.GetProperty("style");
        Assert.Equal(expectedStyleName, updatedStyle.GetProperty("styleName").GetString());
        if (expectedStyleName == "solid")
        {
            Assert.Equal("#AABBCC", updatedStyle.GetProperty("backgroundColor").GetString());
            Assert.Equal("#112233", updatedStyle.GetProperty("textColor").GetString());
            Assert.Equal("#445566", updatedStyle.GetProperty("borderColor").GetString());
        }

        if (expectedStyleName == "gradient")
        {
            Assert.Equal("#ABCDEF", updatedStyle.GetProperty("rightColor").GetString());
            Assert.Equal("none", updatedStyle.GetProperty("borderMode").GetString());
        }
    }

    [Fact]
    public async Task TagUpdate_WithNameOnly_ShouldPreserveEmojiAndStyle()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);
        var originalTag = await CreateTagAsync(client, "Rename source", "🐈‍⬛");

        // Act
        var response = await CallTagUpdateAsync(
            client,
            patToken,
            new
            {
                boardId = 1,
                currentTagName = "rename SOURCE",
                name = "Renamed tag"
            },
            "tag-update-name-only");
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        var updatedTag = McpJsonRpcClient.GetStructuredContent(payload).GetProperty("tag");
        Assert.Equal(originalTag.Id, updatedTag.GetProperty("id").GetInt32());
        Assert.Equal("Renamed tag", updatedTag.GetProperty("name").GetString());
        Assert.Equal("🐈‍⬛", updatedTag.GetProperty("emoji").GetString());
        Assert.Equal(originalTag.StyleName, updatedTag.GetProperty("style").GetProperty("styleName").GetString());
    }

    [Fact]
    public async Task TagUpdate_WithExplicitNullEmoji_ShouldClearEmoji()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);
        await CreateTagAsync(client, "Clear emoji", "🧹");
        var arguments = new Dictionary<string, object?>
        {
            ["boardId"] = 1,
            ["currentTagName"] = "Clear emoji",
            ["emoji"] = null
        };

        // Act
        var response = await CallTagUpdateAsync(
            client,
            patToken,
            arguments,
            "tag-update-clear-emoji");
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        var updatedTag = McpJsonRpcClient.GetStructuredContent(payload).GetProperty("tag");
        Assert.Equal(JsonValueKind.Null, updatedTag.GetProperty("emoji").ValueKind);
    }

    [Fact]
    public async Task TagUpdate_WhenRenamed_ShouldKeepExistingCardAssignment()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);
        var tag = await CreateTagAsync(client, "Assigned before rename", "🔗");
        var boardResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new { name = "board_get", arguments = new { id = 1 } },
            "board-get-before-tag-rename",
            patToken);
        using var boardPayload = await McpJsonRpcClient.ParseJsonAsync(boardResponse);
        var columnId = McpJsonRpcClient.GetStructuredContent(boardPayload)
            .GetProperty("columns")[0]
            .GetProperty("id")
            .GetInt32();
        var createCardResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "card_create",
                arguments = new
                {
                    boardId = 1,
                    columnId,
                    title = "Tag assignment survivor",
                    description = string.Empty,
                    tagNames = new[] { tag.Name }
                }
            },
            "card-create-before-tag-rename",
            patToken);
        using var createCardPayload = await McpJsonRpcClient.ParseJsonAsync(createCardResponse);
        var cardId = McpJsonRpcClient.GetStructuredContent(createCardPayload)
            .GetProperty("card")
            .GetProperty("id")
            .GetInt32();

        // Act
        var updateResponse = await CallTagUpdateAsync(
            client,
            patToken,
            new
            {
                boardId = 1,
                currentTagName = tag.Name,
                name = "Assigned after rename",
                emoji = "✅",
                style = new { styleName = "auto" }
            },
            "tag-update-assigned-rename");
        using var updatePayload = await McpJsonRpcClient.ParseJsonAsync(updateResponse);

        // Assert
        Assert.False(updatePayload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var updatedTag = McpJsonRpcClient.GetStructuredContent(updatePayload).GetProperty("tag");
        Assert.Equal("✅", updatedTag.GetProperty("emoji").GetString());
        Assert.Equal("auto", updatedTag.GetProperty("style").GetProperty("styleName").GetString());
        var cardGetResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new { name = "card_get", arguments = new { boardId = 1, id = cardId } },
            "card-get-after-tag-rename",
            patToken);
        using var cardGetPayload = await McpJsonRpcClient.ParseJsonAsync(cardGetResponse);
        var assignedTag = Assert.Single(McpJsonRpcClient.GetStructuredContent(cardGetPayload).GetProperty("tags").EnumerateArray());
        Assert.Equal(tag.Id, assignedTag.GetProperty("id").GetInt32());
        Assert.Equal("Assigned after rename", assignedTag.GetProperty("name").GetString());
    }

    [Fact]
    public async Task TagUpdate_WithDuplicateName_ShouldReturnNameError()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);
        await CreateTagAsync(client, "Existing destination", null);
        await CreateTagAsync(client, "Duplicate source", null);

        // Act
        var response = await CallTagUpdateAsync(
            client,
            patToken,
            new
            {
                boardId = 1,
                currentTagName = "Duplicate source",
                name = "Existing destination"
            },
            "tag-update-duplicate-name");
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        var validationErrors = McpJsonRpcClient.GetStructuredContent(payload).GetProperty("validationErrors");
        Assert.True(validationErrors.TryGetProperty("name", out _));
    }

    [Fact]
    public async Task TagUpdate_WithNoEditableFields_ShouldReturnValidationError()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);

        // Act
        var response = await CallTagUpdateAsync(
            client,
            patToken,
            new { boardId = 1, currentTagName = "Unused" },
            "tag-update-empty");
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        var error = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal("validation_failed", error.GetProperty("code").GetString());
        Assert.True(error.GetProperty("validationErrors").TryGetProperty(string.Empty, out _));
    }

    [Fact]
    public async Task TagUpdate_WithUnknownTag_ShouldReturnNotFound()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);

        // Act
        var response = await CallTagUpdateAsync(
            client,
            patToken,
            new { boardId = 1, currentTagName = "Does not exist", name = "Still missing" },
            "tag-update-not-found");
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        var error = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal("not_found", error.GetProperty("code").GetString());
        Assert.Equal("Tag not found.", error.GetProperty("message").GetString());
    }

    [Fact]
    public async Task TagUpdate_WithInvalidStyle_ShouldReturnStructuredErrors()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);
        await CreateTagAsync(client, "Invalid update target", null);

        // Act
        var response = await CallTagUpdateAsync(
            client,
            patToken,
            new
            {
                boardId = 1,
                currentTagName = "Invalid update target",
                style = new
                {
                    styleName = "solid",
                    backgroundColor = "blue",
                    textColorMode = "auto",
                    borderMode = "auto"
                }
            },
            "tag-update-invalid-fields");
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        var validationErrors = McpJsonRpcClient.GetStructuredContent(payload).GetProperty("validationErrors");
        Assert.True(validationErrors.TryGetProperty("style", out _));
    }

    [Fact]
    public async Task TagUpdate_WithInvalidEmoji_ShouldReturnEmojiError()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);
        await CreateTagAsync(client, "Invalid emoji target", null);

        // Act
        var response = await CallTagUpdateAsync(
            client,
            patToken,
            new
            {
                boardId = 1,
                currentTagName = "Invalid emoji target",
                emoji = "not emoji"
            },
            "tag-update-invalid-emoji");
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        var validationErrors = McpJsonRpcClient.GetStructuredContent(payload).GetProperty("validationErrors");
        Assert.True(validationErrors.TryGetProperty("emoji", out _));
    }

    [Fact]
    public async Task TagUpdate_WithReadOnlyPat_ShouldReturnForbidden()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client, ["mcp:read"]);

        // Act
        var response = await CallTagUpdateAsync(
            client,
            patToken,
            new { boardId = 1, currentTagName = "Unused", name = "Denied" },
            "tag-update-read-only");
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        var error = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal("forbidden", error.GetProperty("code").GetString());
        Assert.Equal(403, error.GetProperty("statusCode").GetInt32());
    }

    private static Task<HttpResponseMessage> CallTagUpdateAsync(
        HttpClient client,
        string patToken,
        object arguments,
        string requestId) =>
        McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new
            {
                name = "tag_update",
                arguments
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
