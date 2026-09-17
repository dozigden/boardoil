using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Auth;
using BoardOil.Mcp.Contracts;
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
        Assert.Equal(
            ["oauth", "personal_access_token"],
            payload.RootElement.GetProperty("auth").GetProperty("methods")
                .EnumerateArray().Select(method => method.GetString()).ToArray());
        Assert.Equal("oauth", payload.RootElement.GetProperty("setup").GetProperty("preferredAuth").GetString());
        Assert.Equal("personal_access_token", payload.RootElement.GetProperty("setup").GetProperty("manualFallbackAuth").GetString());
        Assert.Equal("/access-tokens", payload.RootElement.GetProperty("setup").GetProperty("patManagementUi").GetString());
        Assert.Equal(
            "/.well-known/oauth-protected-resource/mcp",
            payload.RootElement.GetProperty("setup").GetProperty("oauthProtectedResourceMetadata").GetString());
        Assert.Equal("server/discover", payload.RootElement
            .GetProperty("setup")
            .GetProperty("recommendedFirstCallSequence")[0]
            .GetProperty("method")
            .GetString());
        Assert.Equal("tools/list", payload.RootElement
            .GetProperty("setup")
            .GetProperty("recommendedFirstCallSequence")[1]
            .GetProperty("method")
            .GetString());
        Assert.Equal(ToolNames.IdentityGet, payload.RootElement
            .GetProperty("setup")
            .GetProperty("recommendedFirstCallSequence")[2]
            .GetProperty("tool")
            .GetString());
        Assert.Equal(ToolNames.BoardList, payload.RootElement
            .GetProperty("setup")
            .GetProperty("recommendedFirstCallSequence")[3]
            .GetProperty("tool")
            .GetString());
        Assert.Equal(ToolNames.CardOptionsGet, payload.RootElement
            .GetProperty("setup")
            .GetProperty("recommendedFirstCallSequence")[4]
            .GetProperty("tool")
            .GetString());
        Assert.Equal("tool-first", payload.RootElement.GetProperty("profile").GetProperty("mode").GetString());
        Assert.Equal("supported-empty-list", payload.RootElement.GetProperty("profile").GetProperty("promptsList").GetString());
        Assert.Equal("supported-empty-list", payload.RootElement.GetProperty("profile").GetProperty("resourcesList").GetString());
        Assert.False(payload.RootElement.GetProperty("setup").TryGetProperty("examples", out _));
        Assert.Equal("POST", payload.RootElement.GetProperty("examples").GetProperty("toolsListRequest").GetProperty("method").GetString());
    }

    [Fact]
    public async Task WellKnownMcp_WithConfiguredPublicBaseUrl_ShouldReturnAbsoluteMetadataUrls()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var putResponse = await client.PutAsJsonAsync("/api/system/configuration", new UpdateConfigurationRequest("https://boardoil.example.com/base"));
        putResponse.EnsureSuccessStatusCode();

        // Act
        var response = await client.GetAsync("/.well-known/mcp");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("https://boardoil.example.com/base/mcp", payload.RootElement.GetProperty("endpoint").GetString());
        Assert.Equal(
            "https://boardoil.example.com/base/mcp",
            payload.RootElement.GetProperty("auth").GetProperty("oauth").GetProperty("resource").GetString());
        Assert.Equal(
            "bo_pat_",
            payload.RootElement.GetProperty("auth").GetProperty("personalAccessToken").GetProperty("tokenPrefix").GetString());
        Assert.Equal(
            "https://boardoil.example.com/base/access-tokens",
            payload.RootElement.GetProperty("setup").GetProperty("patManagementUi").GetString());
        Assert.Equal(
            "https://boardoil.example.com/base/access-tokens",
            payload.RootElement.GetProperty("auth").GetProperty("personalAccessToken").GetProperty("managementUi").GetString());
        Assert.Equal(
            "https://boardoil.example.com/base/.well-known/oauth-protected-resource/mcp",
            payload.RootElement.GetProperty("auth").GetProperty("oauth").GetProperty("protectedResourceMetadata").GetString());
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
        Assert.Equal("server/discover", payload.RootElement
            .GetProperty("examples")
            .GetProperty("serverDiscoverRequest")
            .GetProperty("body")
            .GetProperty("method")
            .GetString());
        var toolsListExample = payload.RootElement
            .GetProperty("examples")
            .GetProperty("toolsListRequest");
        Assert.Equal("2026-07-28", toolsListExample
            .GetProperty("headers")
            .GetProperty("MCP-Protocol-Version")
            .GetString());
        Assert.Equal("tools/list", toolsListExample
            .GetProperty("headers")
            .GetProperty("Mcp-Method")
            .GetString());
        Assert.Equal("2026-07-28", toolsListExample
            .GetProperty("body")
            .GetProperty("params")
            .GetProperty("_meta")
            .GetProperty("io.modelcontextprotocol/protocolVersion")
            .GetString());
        Assert.Equal("tools/call", payload.RootElement
            .GetProperty("examples")
            .GetProperty("boardListRequest")
            .GetProperty("body")
            .GetProperty("method")
            .GetString());
        Assert.Equal(ToolNames.BoardList, payload.RootElement
            .GetProperty("examples")
            .GetProperty("boardListRequest")
            .GetProperty("headers")
            .GetProperty("Mcp-Name")
            .GetString());
        Assert.Equal(ToolNames.BoardList, payload.RootElement
            .GetProperty("examples")
            .GetProperty("boardListRequest")
            .GetProperty("body")
            .GetProperty("params")
            .GetProperty("name")
            .GetString());
        Assert.Equal(ToolNames.IdentityGet, payload.RootElement
            .GetProperty("examples")
            .GetProperty("identityGetRequest")
            .GetProperty("body")
            .GetProperty("params")
            .GetProperty("name")
            .GetString());
        Assert.Equal(ToolNames.CardOptionsGet, payload.RootElement
            .GetProperty("examples")
            .GetProperty("cardOptionsGetRequest")
            .GetProperty("body")
            .GetProperty("params")
            .GetProperty("name")
            .GetString());
    }

    [Fact]
    public async Task ToolsList_WithReadScope_ShouldAdvertiseOnlyIdentityAndReadTools()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client, [MachinePatScopes.McpRead]);

        // Act
        var response = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/list",
            new { },
            "tools-list-read-scope",
            patToken);
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            [
                ToolNames.IdentityGet,
                ToolNames.BoardList,
                ToolNames.BoardGet,
                ToolNames.CardSearch,
                ToolNames.CardGet,
                ToolNames.CardOptionsGet,
                ToolNames.CardAttachmentDownload
            ],
            GetToolNames(payload));
    }

    [Fact]
    public async Task ToolsList_WithWriteScope_ShouldAdvertiseCompleteCatalogue()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client, [MachinePatScopes.McpWrite]);

        // Act
        var response = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/list",
            new { },
            "tools-list-write-scope",
            patToken);
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(21, GetToolNames(payload).Length);
    }

    [Fact]
    public async Task BoardList_WithWriteScope_ShouldSucceed()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client, [MachinePatScopes.McpWrite]);

        // Act
        var response = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            new { name = ToolNames.BoardList, arguments = new { } },
            "board-list-write-scope",
            patToken);
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
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
        Assert.Equal(
            [
                ToolNames.IdentityGet,
                ToolNames.BoardList,
                ToolNames.BoardGet,
                ToolNames.CardSearch,
                ToolNames.CardGet,
                ToolNames.CardOptionsGet,
                ToolNames.CardCreate,
                ToolNames.CardUpdate,
                ToolNames.CardMove,
                ToolNames.CardCommentCreate,
                ToolNames.CardArchive,
                ToolNames.CardDelete,
                ToolNames.CardAttachmentUpload,
                ToolNames.CardAttachmentDownload,
                ToolNames.CardAttachmentDelete,
                ToolNames.TagCreate,
                ToolNames.TagUpdate,
                ToolNames.TagDelete,
                ToolNames.SlickCreate,
                ToolNames.SlickUpdate,
                ToolNames.SlickDelete
            ],
            toolNames);
        Assert.DoesNotContain("card_attachment_list", toolNames);
        Assert.DoesNotContain("columns_list", toolNames);
        Assert.DoesNotContain("card.move_by_column_name", toolNames);

        foreach (var (toolName, outcome) in new[]
        {
            (ToolNames.CardCreate, "created"),
            (ToolNames.CardUpdate, "updated"),
            (ToolNames.CardMove, "moved"),
            (ToolNames.CardArchive, "archived"),
            (ToolNames.CardDelete, "deleted"),
            (ToolNames.CardCommentCreate, "created")
        })
        {
            var schema = McpJsonRpcClient.GetToolByName(toolsListPayload, toolName).GetProperty("outputSchema");
            Assert.Equal("object", schema.GetProperty("type").GetString());
            Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
            Assert.Equal(["id", "outcome"], schema.GetProperty("required").EnumerateArray()
                .Select(value => value.GetString()).ToArray());
            var properties = schema.GetProperty("properties");
            Assert.Equal(["id", "outcome"], properties.EnumerateObject().Select(property => property.Name).ToArray());
            Assert.Equal("integer", properties.GetProperty("id").GetProperty("type").GetString());
            Assert.Equal(1, properties.GetProperty("id").GetProperty("minimum").GetInt32());
            Assert.Equal("string", properties.GetProperty("outcome").GetProperty("type").GetString());
            Assert.Equal([outcome], properties.GetProperty("outcome").GetProperty("enum").EnumerateArray()
                .Select(value => value.GetString()).ToArray());
        }

        AssertToolAnnotations(toolsListPayload, ToolNames.IdentityGet, readOnly: true, destructive: false, idempotent: true);
        AssertToolAnnotations(toolsListPayload, ToolNames.BoardList, readOnly: true, destructive: false, idempotent: true);
        AssertToolAnnotations(toolsListPayload, ToolNames.BoardGet, readOnly: true, destructive: false, idempotent: true);
        AssertToolAnnotations(toolsListPayload, ToolNames.CardSearch, readOnly: true, destructive: false, idempotent: true);
        AssertToolAnnotations(toolsListPayload, ToolNames.CardGet, readOnly: true, destructive: false, idempotent: true);
        AssertToolAnnotations(toolsListPayload, ToolNames.CardOptionsGet, readOnly: true, destructive: false, idempotent: true);
        AssertToolAnnotations(toolsListPayload, ToolNames.CardCreate, readOnly: false, destructive: false, idempotent: false);
        AssertToolAnnotations(toolsListPayload, ToolNames.CardUpdate, readOnly: false, destructive: true, idempotent: true);
        AssertToolAnnotations(toolsListPayload, ToolNames.CardMove, readOnly: false, destructive: false, idempotent: false);
        AssertToolAnnotations(toolsListPayload, ToolNames.CardCommentCreate, readOnly: false, destructive: false, idempotent: false);
        AssertToolAnnotations(toolsListPayload, ToolNames.CardArchive, readOnly: false, destructive: true, idempotent: false);
        AssertToolAnnotations(toolsListPayload, ToolNames.CardDelete, readOnly: false, destructive: true, idempotent: true);
        AssertToolAnnotations(toolsListPayload, ToolNames.CardAttachmentUpload, readOnly: false, destructive: false, idempotent: false);
        AssertToolAnnotations(toolsListPayload, ToolNames.CardAttachmentDownload, readOnly: true, destructive: false, idempotent: true);
        AssertToolAnnotations(toolsListPayload, ToolNames.CardAttachmentDelete, readOnly: false, destructive: true, idempotent: true);
        AssertToolAnnotations(toolsListPayload, ToolNames.TagCreate, readOnly: false, destructive: false, idempotent: true);
        AssertToolAnnotations(toolsListPayload, ToolNames.TagUpdate, readOnly: false, destructive: true, idempotent: true);
        AssertToolAnnotations(toolsListPayload, ToolNames.TagDelete, readOnly: false, destructive: true, idempotent: true);
        AssertToolAnnotations(toolsListPayload, ToolNames.SlickCreate, readOnly: false, destructive: false, idempotent: true);
        AssertToolAnnotations(toolsListPayload, ToolNames.SlickUpdate, readOnly: false, destructive: true, idempotent: true);
        AssertToolAnnotations(toolsListPayload, ToolNames.SlickDelete, readOnly: false, destructive: true, idempotent: true);

        AssertMaintenanceToolDescription(toolsListPayload, ToolNames.TagCreate);
        AssertMaintenanceToolDescription(toolsListPayload, ToolNames.TagUpdate);
        AssertMaintenanceToolDescription(toolsListPayload, ToolNames.TagDelete);
        AssertMaintenanceToolDescription(toolsListPayload, ToolNames.SlickCreate);
        AssertMaintenanceToolDescription(toolsListPayload, ToolNames.SlickUpdate);
        AssertMaintenanceToolDescription(toolsListPayload, ToolNames.SlickDelete);

        var identityGetTool = McpJsonRpcClient.GetToolByName(toolsListPayload, ToolNames.IdentityGet);
        Assert.Empty(identityGetTool.GetProperty("inputSchema").GetProperty("properties").EnumerateObject());
        var identityUserSchema = identityGetTool
            .GetProperty("outputSchema")
            .GetProperty("properties")
            .GetProperty("user")
            .GetProperty("properties");
        Assert.False(identityUserSchema.TryGetProperty("email", out _));
        Assert.False(identityUserSchema.TryGetProperty("identityType", out _));
        Assert.False(identityUserSchema.TryGetProperty("isActive", out _));

        var cardOptionsGetTool = McpJsonRpcClient.GetToolByName(toolsListPayload, ToolNames.CardOptionsGet);
        Assert.Contains("active assignees", cardOptionsGetTool.GetProperty("description").GetString(), StringComparison.Ordinal);
        var cardOptionsProperties = cardOptionsGetTool.GetProperty("outputSchema").GetProperty("properties");
        Assert.True(cardOptionsProperties.TryGetProperty("columns", out _));
        Assert.True(cardOptionsProperties.TryGetProperty("members", out var membersSchema));
        Assert.True(cardOptionsProperties.TryGetProperty("cardTypes", out _));
        Assert.True(cardOptionsProperties.TryGetProperty("defaultCardTypeId", out _));
        Assert.True(cardOptionsProperties.TryGetProperty("tags", out var tagsSchema));
        Assert.True(cardOptionsProperties.TryGetProperty("slicks", out var slicksSchema));
        Assert.False(membersSchema.GetProperty("items").GetProperty("properties").TryGetProperty("isActive", out _));
        var tagProperties = tagsSchema.GetProperty("items").GetProperty("properties");
        Assert.True(tagProperties.TryGetProperty("id", out _));
        Assert.True(tagProperties.TryGetProperty("name", out _));
        Assert.True(tagProperties.TryGetProperty("emoji", out _));
        Assert.True(tagProperties.TryGetProperty("style", out var tagStyleSchema));
        var cardOptionStyleNames = tagStyleSchema.GetProperty("oneOf")
            .EnumerateArray()
            .Select(variant => variant.GetProperty("properties").GetProperty("styleName").GetProperty("const").GetString()!)
            .ToArray();
        Assert.Equal(["auto", "presets", "solid", "gradient"], cardOptionStyleNames);
        Assert.DoesNotContain("stylePropertiesJson", tagsSchema.GetRawText(), StringComparison.Ordinal);
        var cardOptionSlickProperties = slicksSchema.GetProperty("items").GetProperty("properties");
        Assert.True(cardOptionSlickProperties.TryGetProperty("id", out _));
        Assert.True(cardOptionSlickProperties.TryGetProperty("name", out _));
        Assert.True(cardOptionSlickProperties.TryGetProperty("style", out var slickOptionStyleSchema));
        var cardOptionSlickStyleNames = slickOptionStyleSchema.GetProperty("oneOf")
            .EnumerateArray()
            .Select(variant => variant.GetProperty("properties").GetProperty("styleName").GetProperty("const").GetString()!)
            .ToArray();
        Assert.Equal(["presets", "solid"], cardOptionSlickStyleNames);
        Assert.DoesNotContain("stylePropertiesJson", slicksSchema.GetRawText(), StringComparison.Ordinal);

        var searchTool = McpJsonRpcClient.GetToolByName(toolsListPayload, ToolNames.CardSearch);
        var searchInput = searchTool.GetProperty("inputSchema");
        Assert.Equal(["boardId", "query"], searchInput.GetProperty("required").EnumerateArray().Select(p => p.GetString()).ToArray());
        Assert.False(searchInput.GetProperty("additionalProperties").GetBoolean());
        var searchInputProperties = searchInput.GetProperty("properties");
        Assert.Equal(["boardId", "query", "offset", "limit", "archived"], searchInputProperties.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(0, searchInputProperties.GetProperty("offset").GetProperty("default").GetInt32());
        Assert.Equal(20, searchInputProperties.GetProperty("limit").GetProperty("default").GetInt32());
        Assert.Equal(100, searchInputProperties.GetProperty("limit").GetProperty("maximum").GetInt32());
        Assert.False(searchInputProperties.GetProperty("archived").GetProperty("default").GetBoolean());
        var searchOutput = searchTool.GetProperty("outputSchema");
        var searchOutputVariants = searchOutput.GetProperty("anyOf").EnumerateArray().ToArray();
        Assert.Equal(2, searchOutputVariants.Length);
        foreach (var variant in searchOutputVariants)
        {
            Assert.False(variant.GetProperty("additionalProperties").GetBoolean());
            Assert.Equal(["cards", "totalCount", "offset", "limit"],
                variant.GetProperty("required").EnumerateArray().Select(p => p.GetString()).ToArray());
        }
        var liveSummarySchema = searchOutputVariants[0].GetProperty("properties").GetProperty("cards").GetProperty("items");
        Assert.False(liveSummarySchema.GetProperty("additionalProperties").GetBoolean());
        string[] liveSummaryFields = ["id", "title", "columnId", "cardTypeId", "externalUrl", "tagNames", "slickName"];
        Assert.Equal(liveSummaryFields, liveSummarySchema.GetProperty("required").EnumerateArray().Select(p => p.GetString()).ToArray());
        Assert.Equal(liveSummaryFields, liveSummarySchema.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToArray());
        var archivedSummarySchema = searchOutputVariants[1].GetProperty("properties").GetProperty("cards").GetProperty("items");
        string[] archivedSummaryFields = ["id", "title", "tagNames", "archivedAtUtc"];
        Assert.Equal(archivedSummaryFields, archivedSummarySchema.GetProperty("required").EnumerateArray().Select(p => p.GetString()).ToArray());
        Assert.Equal(archivedSummaryFields, archivedSummarySchema.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToArray());

        var boardListTool = McpJsonRpcClient.GetToolByName(toolsListPayload, ToolNames.BoardList);
        Assert.True(boardListTool.GetProperty("inputSchema").TryGetProperty("properties", out var boardListProperties));
        Assert.Empty(boardListProperties.EnumerateObject());

        var boardGetTool = McpJsonRpcClient.GetToolByName(toolsListPayload, ToolNames.BoardGet);
        var boardGetProperties = boardGetTool.GetProperty("inputSchema").GetProperty("properties");
        Assert.True(boardGetProperties.TryGetProperty("id", out _));
        Assert.False(boardGetProperties.TryGetProperty("boardId", out _));

        var cardMoveTool = McpJsonRpcClient.GetToolByName(toolsListPayload, ToolNames.CardMove);
        var cardMoveProperties = cardMoveTool.GetProperty("inputSchema").GetProperty("properties");
        Assert.True(cardMoveProperties.TryGetProperty("id", out _));
        Assert.True(cardMoveProperties.TryGetProperty("columnId", out _));
        Assert.True(cardMoveProperties.TryGetProperty("afterId", out _));
        Assert.False(cardMoveProperties.TryGetProperty("cardId", out _));
        Assert.False(cardMoveProperties.TryGetProperty("boardColumnId", out _));
        Assert.False(cardMoveProperties.TryGetProperty("positionAfterCardId", out _));
        Assert.Contains(
            "card_options_get.columns[].id",
            cardMoveProperties.GetProperty("columnId").GetProperty("description").GetString(),
            StringComparison.Ordinal);
        Assert.Contains("card_options_get", cardMoveTool.GetProperty("description").GetString(), StringComparison.Ordinal);

        var cardCreateTool = McpJsonRpcClient.GetToolByName(toolsListPayload, ToolNames.CardCreate);
        var cardCreateProperties = cardCreateTool.GetProperty("inputSchema").GetProperty("properties");
        Assert.True(cardCreateProperties.TryGetProperty("cardTypeId", out _));
        Assert.True(cardCreateProperties.TryGetProperty("assignedUserId", out _));
        Assert.True(cardCreateProperties.TryGetProperty("slickName", out _));
        Assert.True(cardCreateProperties.TryGetProperty("externalUrl", out _));
        Assert.Contains(
            "card_options_get.cardTypes[].id",
            cardCreateProperties.GetProperty("cardTypeId").GetProperty("description").GetString(),
            StringComparison.Ordinal);
        Assert.Contains("card_options_get", cardCreateTool.GetProperty("description").GetString(), StringComparison.Ordinal);
        var cardCreateRequired = cardCreateTool.GetProperty("inputSchema").GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.DoesNotContain("cardTypeId", cardCreateRequired);
        Assert.DoesNotContain("assignedUserId", cardCreateRequired);
        Assert.DoesNotContain("slickName", cardCreateRequired);
        Assert.DoesNotContain("externalUrl", cardCreateRequired);
        Assert.Contains("card_attachment_upload",
            cardCreateProperties.GetProperty("description").GetProperty("description").GetString(),
            StringComparison.Ordinal);

        var cardGetTool = McpJsonRpcClient.GetToolByName(toolsListPayload, ToolNames.CardGet);
        var cardGetProperties = cardGetTool.GetProperty("inputSchema").GetProperty("properties");
        Assert.True(cardGetProperties.TryGetProperty("boardId", out _));
        Assert.True(cardGetProperties.TryGetProperty("id", out _));
        Assert.False(cardGetProperties.GetProperty("archived").GetProperty("default").GetBoolean());
        var cardGetOutputVariants = cardGetTool.GetProperty("outputSchema").GetProperty("oneOf").EnumerateArray().ToArray();
        Assert.Equal(2, cardGetOutputVariants.Length);
        Assert.DoesNotContain("archivedAtUtc", cardGetOutputVariants[0].GetProperty("required")
            .EnumerateArray().Select(value => value.GetString()));
        Assert.Contains("archivedAtUtc", cardGetOutputVariants[1].GetProperty("required")
            .EnumerateArray().Select(value => value.GetString()));
        var archivedCommentSchema = cardGetTool.GetProperty("outputSchema").GetProperty("$defs").GetProperty("archivedComment");
        Assert.False(archivedCommentSchema.GetProperty("properties").TryGetProperty("id", out _));
        Assert.False(archivedCommentSchema.GetProperty("properties").TryGetProperty("cardId", out _));

        var attachmentDeleteTool = McpJsonRpcClient.GetToolByName(toolsListPayload, ToolNames.CardAttachmentDelete);
        var attachmentDeleteInput = attachmentDeleteTool.GetProperty("inputSchema");
        Assert.Equal(["boardId", "cardId", "id"], attachmentDeleteInput.GetProperty("required")
            .EnumerateArray().Select(value => value.GetString()).ToArray());
        Assert.False(attachmentDeleteInput.GetProperty("properties").TryGetProperty("archived", out _));
        Assert.False(attachmentDeleteInput.GetProperty("additionalProperties").GetBoolean());

        var downloadTool = McpJsonRpcClient.GetToolByName(toolsListPayload, ToolNames.CardAttachmentDownload);
        Assert.Equal(["boardId", "id"], downloadTool.GetProperty("inputSchema").GetProperty("required")
            .EnumerateArray().Select(value => value.GetString()).ToArray());
        Assert.Equal(["url", "method", "headers", "expiresAtUtc"], downloadTool.GetProperty("outputSchema").GetProperty("required")
            .EnumerateArray().Select(value => value.GetString()).ToArray());

        var uploadTool = McpJsonRpcClient.GetToolByName(toolsListPayload, ToolNames.CardAttachmentUpload);
        Assert.Equal(["boardId", "cardId", "fileName", "byteLength"], uploadTool.GetProperty("inputSchema").GetProperty("required")
            .EnumerateArray().Select(value => value.GetString()).ToArray());
        Assert.Equal(["url", "method", "headers", "byteLength", "markdownSnippet", "expiresAtUtc"],
            uploadTool.GetProperty("outputSchema").GetProperty("required")
                .EnumerateArray().Select(value => value.GetString()).ToArray());
        Assert.Contains("after the HTTP PUT succeeds",
            uploadTool.GetProperty("outputSchema").GetProperty("properties")
                .GetProperty("markdownSnippet").GetProperty("description").GetString(),
            StringComparison.OrdinalIgnoreCase);

        var cardUpdateTool = McpJsonRpcClient.GetToolByName(toolsListPayload, ToolNames.CardUpdate);
        var cardUpdateProperties = cardUpdateTool.GetProperty("inputSchema").GetProperty("properties");
        Assert.True(cardUpdateProperties.TryGetProperty("columnId", out _));
        Assert.True(cardUpdateProperties.TryGetProperty("cardTypeId", out _));
        Assert.True(cardUpdateProperties.TryGetProperty("assignedUserId", out _));
        Assert.True(cardUpdateProperties.TryGetProperty("slickName", out _));
        Assert.True(cardUpdateProperties.TryGetProperty("externalUrl", out _));
        Assert.Contains(
            "card_options_get.members[].userId",
            cardUpdateProperties.GetProperty("assignedUserId").GetProperty("description").GetString(),
            StringComparison.Ordinal);
        Assert.Contains("card_options_get", cardUpdateTool.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Contains("card_attachment_upload",
            cardUpdateProperties.GetProperty("description").GetProperty("description").GetString(),
            StringComparison.Ordinal);
        var cardUpdateRequired = cardUpdateTool.GetProperty("inputSchema").GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.DoesNotContain("columnId", cardUpdateRequired);
        Assert.Contains("cardTypeId", cardUpdateRequired);
        Assert.DoesNotContain("assignedUserId", cardUpdateRequired);
        Assert.Contains("slickName", cardUpdateRequired);
        Assert.Contains("externalUrl", cardUpdateRequired);

        var cardCommentCreateTool = McpJsonRpcClient.GetToolByName(toolsListPayload, ToolNames.CardCommentCreate);
        var cardCommentCreateProperties = cardCommentCreateTool.GetProperty("inputSchema").GetProperty("properties");
        Assert.True(cardCommentCreateProperties.TryGetProperty("boardId", out _));
        Assert.True(cardCommentCreateProperties.TryGetProperty("id", out _));
        Assert.True(cardCommentCreateProperties.TryGetProperty("text", out _));

        var cardArchiveTool = McpJsonRpcClient.GetToolByName(toolsListPayload, ToolNames.CardArchive);
        Assert.Equal(["boardId", "id"], cardArchiveTool.GetProperty("inputSchema").GetProperty("required")
            .EnumerateArray().Select(value => value.GetString()).ToArray());
        Assert.Contains("from the active board", cardArchiveTool.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Contains("restored if needed", cardArchiveTool.GetProperty("description").GetString(), StringComparison.Ordinal);
        var cardDeleteTool = McpJsonRpcClient.GetToolByName(toolsListPayload, ToolNames.CardDelete);
        Assert.Contains("Permanently delete", cardDeleteTool.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Contains(ToolNames.CardArchive, cardDeleteTool.GetProperty("description").GetString(), StringComparison.Ordinal);

        var tagCreateTool = McpJsonRpcClient.GetToolByName(toolsListPayload, ToolNames.TagCreate);
        var tagCreateInputSchema = tagCreateTool.GetProperty("inputSchema");
        var tagCreateProperties = tagCreateInputSchema.GetProperty("properties");
        Assert.True(tagCreateProperties.TryGetProperty("boardId", out _));
        Assert.True(tagCreateProperties.TryGetProperty("name", out _));
        Assert.True(tagCreateProperties.TryGetProperty("emoji", out _));
        Assert.True(tagCreateProperties.TryGetProperty("style", out var tagCreateStyleSchema));
        Assert.Equal(
            ["boardId", "name", "emoji", "style"],
            tagCreateInputSchema.GetProperty("required").EnumerateArray().Select(value => value.GetString()).ToArray());

        var tagUpdateTool = McpJsonRpcClient.GetToolByName(toolsListPayload, ToolNames.TagUpdate);
        var tagUpdateInputSchema = tagUpdateTool.GetProperty("inputSchema");
        var tagUpdateProperties = tagUpdateInputSchema.GetProperty("properties");
        Assert.True(tagUpdateProperties.TryGetProperty("boardId", out _));
        Assert.True(tagUpdateProperties.TryGetProperty("currentTagName", out _));
        Assert.True(tagUpdateProperties.TryGetProperty("name", out _));
        Assert.True(tagUpdateProperties.TryGetProperty("emoji", out var emojiSchema));
        Assert.True(tagUpdateProperties.TryGetProperty("style", out var styleSchema));
        Assert.Contains("null", emojiSchema.GetProperty("type").EnumerateArray().Select(type => type.GetString()));
        var styleNames = styleSchema.GetProperty("oneOf")
            .EnumerateArray()
            .Select(variant => variant.GetProperty("properties").GetProperty("styleName").GetProperty("const").GetString())
            .ToArray();
        Assert.Equal(["auto", "presets", "solid", "gradient"], styleNames);
        Assert.Equal(tagCreateStyleSchema.GetRawText(), styleSchema.GetRawText());
        var solidStyleSchema = styleSchema.GetProperty("oneOf")[2];
        var textColorTypes = solidStyleSchema.GetProperty("properties").GetProperty("textColor").GetProperty("type")
            .EnumerateArray()
            .Select(type => type.GetString())
            .ToArray();
        Assert.Contains("null", textColorTypes);
        var tagUpdateRequired = tagUpdateInputSchema.GetProperty("required").EnumerateArray().Select(value => value.GetString()).ToArray();
        Assert.Equal(["boardId", "currentTagName"], tagUpdateRequired);
        Assert.DoesNotContain("stylePropertiesJson", tagUpdateInputSchema.GetRawText(), StringComparison.Ordinal);
        var tagUpdateOutputSchema = tagUpdateTool.GetProperty("outputSchema");
        Assert.DoesNotContain("stylePropertiesJson", tagUpdateOutputSchema.GetRawText(), StringComparison.Ordinal);
        var tagUpdateOutputStyleSchema = tagUpdateOutputSchema
            .GetProperty("properties")
            .GetProperty("tag")
            .GetProperty("properties")
            .GetProperty("style");
        Assert.Equal(tagStyleSchema.GetRawText(), tagUpdateOutputStyleSchema.GetRawText());

        var tagCreateOutputSchema = tagCreateTool.GetProperty("outputSchema");
        var tagCreateOutputStyleSchema = tagCreateOutputSchema
            .GetProperty("properties")
            .GetProperty("tag")
            .GetProperty("properties")
            .GetProperty("style");
        Assert.Equal(tagStyleSchema.GetRawText(), tagCreateOutputStyleSchema.GetRawText());
        Assert.Equal(
            ["created", "existing"],
            tagCreateOutputSchema.GetProperty("properties").GetProperty("outcome").GetProperty("enum")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray());

        var tagDeleteTool = McpJsonRpcClient.GetToolByName(toolsListPayload, ToolNames.TagDelete);
        var tagDeleteProperties = tagDeleteTool.GetProperty("inputSchema").GetProperty("properties");
        Assert.True(tagDeleteProperties.TryGetProperty("boardId", out _));
        Assert.True(tagDeleteProperties.TryGetProperty("id", out var tagDeleteIdSchema));
        Assert.Contains(
            "card_options_get.tags[].id",
            tagDeleteIdSchema.GetProperty("description").GetString(),
            StringComparison.Ordinal);
        Assert.Equal(
            ["deleted"],
            tagDeleteTool.GetProperty("outputSchema").GetProperty("properties").GetProperty("outcome").GetProperty("enum")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray());

        var slickCreateTool = McpJsonRpcClient.GetToolByName(toolsListPayload, ToolNames.SlickCreate);
        var slickCreateInputSchema = slickCreateTool.GetProperty("inputSchema");
        Assert.Equal(
            ["boardId", "name", "style"],
            slickCreateInputSchema.GetProperty("required").EnumerateArray().Select(value => value.GetString()).ToArray());
        var slickCreateStyleSchema = slickCreateInputSchema.GetProperty("properties").GetProperty("style");
        var slickCreateStyleNames = slickCreateStyleSchema.GetProperty("oneOf")
            .EnumerateArray()
            .Select(variant => variant.GetProperty("properties").GetProperty("styleName").GetProperty("const").GetString()!)
            .ToArray();
        Assert.Equal(["presets", "solid"], slickCreateStyleNames);
        Assert.DoesNotContain("stylePropertiesJson", slickCreateInputSchema.GetRawText(), StringComparison.Ordinal);

        var slickUpdateTool = McpJsonRpcClient.GetToolByName(toolsListPayload, ToolNames.SlickUpdate);
        var slickUpdateInputSchema = slickUpdateTool.GetProperty("inputSchema");
        Assert.Equal(
            ["boardId", "id"],
            slickUpdateInputSchema.GetProperty("required").EnumerateArray().Select(value => value.GetString()).ToArray());
        Assert.Equal(
            slickCreateStyleSchema.GetRawText(),
            slickUpdateInputSchema.GetProperty("properties").GetProperty("style").GetRawText());
        Assert.Contains(
            "card_options_get.slicks[].id",
            slickUpdateInputSchema.GetProperty("properties").GetProperty("id").GetProperty("description").GetString(),
            StringComparison.Ordinal);

        var slickDeleteTool = McpJsonRpcClient.GetToolByName(toolsListPayload, ToolNames.SlickDelete);
        Assert.Contains(
            "card_options_get.slicks[].id",
            slickDeleteTool.GetProperty("inputSchema").GetProperty("properties").GetProperty("id").GetProperty("description").GetString(),
            StringComparison.Ordinal);
        Assert.Equal(
            ["deleted"],
            slickDeleteTool.GetProperty("outputSchema").GetProperty("properties").GetProperty("outcome").GetProperty("enum")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray());
    }

    private static void AssertToolAnnotations(
        JsonDocument toolsListPayload,
        string toolName,
        bool readOnly,
        bool destructive,
        bool idempotent)
    {
        var tool = McpJsonRpcClient.GetToolByName(toolsListPayload, toolName);
        var annotations = tool.GetProperty("annotations");

        Assert.Equal(readOnly, annotations.GetProperty("readOnlyHint").GetBoolean());
        Assert.Equal(destructive, annotations.GetProperty("destructiveHint").GetBoolean());
        Assert.Equal(idempotent, annotations.GetProperty("idempotentHint").GetBoolean());
        Assert.False(annotations.TryGetProperty("openWorldHint", out _));
        Assert.False(annotations.TryGetProperty("title", out _));
        Assert.False(tool.TryGetProperty("title", out _));
    }

    private static void AssertMaintenanceToolDescription(JsonDocument toolsListPayload, string toolName)
    {
        var description = McpJsonRpcClient.GetToolByName(toolsListPayload, toolName)
            .GetProperty("description")
            .GetString();

        Assert.Contains("reusable", description, StringComparison.Ordinal);
        Assert.Contains("card_update", description, StringComparison.Ordinal);
    }

    private static string?[] GetToolNames(JsonDocument toolsListPayload) =>
        toolsListPayload.RootElement
            .GetProperty("result")
            .GetProperty("tools")
            .EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString())
            .ToArray();

    private sealed record UpdateConfigurationRequest(
        string? McpPublicBaseUrl,
        bool OAuthLifecycleDiagnosticsEnabled = false);
}
