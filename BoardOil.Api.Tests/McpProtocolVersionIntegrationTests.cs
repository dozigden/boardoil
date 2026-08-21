using System.Net;
using BoardOil.Api.Tests.Infrastructure;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpProtocolVersionIntegrationTests : McpIntegrationTestBase
{
    [Fact]
    public async Task ServerDiscover_WithModernProtocol_ShouldReturnCapabilitiesWithoutSession()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);

        // Act
        var response = await McpJsonRpcClient.SendRequestAsync(
            client,
            "server/discover",
            new { },
            "modern-discover",
            patToken);
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Mcp-Session-Id"));
        var result = payload.RootElement.GetProperty("result");
        Assert.Contains(
            McpJsonRpcClient.ModernProtocolVersion,
            result.GetProperty("supportedVersions").EnumerateArray().Select(version => version.GetString()));
        Assert.Equal("complete", result.GetProperty("resultType").GetString());
        Assert.Equal(0, result.GetProperty("ttlMs").GetInt32());
        Assert.Equal("private", result.GetProperty("cacheScope").GetString());
        Assert.True(result.GetProperty("capabilities").TryGetProperty("tools", out _));
        Assert.True(result.GetProperty("capabilities").TryGetProperty("prompts", out _));
        Assert.True(result.GetProperty("capabilities").TryGetProperty("resources", out _));
        var serverInfo = result
            .GetProperty("_meta")
            .GetProperty("io.modelcontextprotocol/serverInfo");
        Assert.Equal("BoardOil MCP", serverInfo.GetProperty("name").GetString());
    }

    [Fact]
    public async Task ToolsList_WithModernProtocol_ShouldBeIndependentAndSessionless()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);

        // Act
        var firstResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/list",
            new { },
            "modern-tools-first",
            patToken);
        var secondResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/list",
            new { },
            "modern-tools-second",
            patToken);
        using var firstPayload = await McpJsonRpcClient.ParseJsonAsync(firstResponse);
        using var secondPayload = await McpJsonRpcClient.ParseJsonAsync(secondResponse);

        // Assert
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.False(firstResponse.Headers.Contains("Mcp-Session-Id"));
        Assert.False(secondResponse.Headers.Contains("Mcp-Session-Id"));
        var firstResult = firstPayload.RootElement.GetProperty("result");
        var secondResult = secondPayload.RootElement.GetProperty("result");
        Assert.Equal("complete", firstResult.GetProperty("resultType").GetString());
        Assert.Equal(0, firstResult.GetProperty("ttlMs").GetInt32());
        Assert.Equal("private", firstResult.GetProperty("cacheScope").GetString());
        Assert.Equal(
            firstResult.GetProperty("tools").GetArrayLength(),
            secondResult.GetProperty("tools").GetArrayLength());
    }

    [Fact]
    public async Task ToolsList_ModernAndLegacyProtocols_ShouldReturnSameCatalogueWithEraSpecificShape()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);

        // Act
        var initializeResponse = await McpJsonRpcClient.SendLegacyInitializeAsync(
            client,
            "legacy-initialize",
            patToken);
        var toolsResponse = await McpJsonRpcClient.SendLegacyRequestAsync(
            client,
            "tools/list",
            new { },
            "legacy-tools",
            patToken);
        var modernToolsResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/list",
            new { },
            "modern-tools-comparison",
            patToken);
        using var initializePayload = await McpJsonRpcClient.ParseJsonAsync(initializeResponse);
        using var toolsPayload = await McpJsonRpcClient.ParseJsonAsync(toolsResponse);
        using var modernToolsPayload = await McpJsonRpcClient.ParseJsonAsync(modernToolsResponse);

        // Assert
        Assert.Equal(HttpStatusCode.OK, initializeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, toolsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, modernToolsResponse.StatusCode);
        Assert.False(initializeResponse.Headers.Contains("Mcp-Session-Id"));
        Assert.False(toolsResponse.Headers.Contains("Mcp-Session-Id"));
        Assert.Equal(
            McpJsonRpcClient.LegacyProtocolVersion,
            initializePayload.RootElement.GetProperty("result").GetProperty("protocolVersion").GetString());
        var toolsResult = toolsPayload.RootElement.GetProperty("result");
        Assert.NotEmpty(toolsResult.GetProperty("tools").EnumerateArray());
        Assert.Equal(
            modernToolsPayload.RootElement.GetProperty("result").GetProperty("tools").GetRawText(),
            toolsResult.GetProperty("tools").GetRawText());
        Assert.False(toolsResult.TryGetProperty("resultType", out _));
        Assert.False(toolsResult.TryGetProperty("ttlMs", out _));
        Assert.False(toolsResult.TryGetProperty("cacheScope", out _));
    }

    [Fact]
    public async Task ToolCalls_ModernAndLegacyProtocols_ShouldReturnSameResultsAndErrors()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);
        var identityParams = new { name = "identity_get", arguments = new { } };
        var unknownToolParams = new { name = "unknown_tool", arguments = new { } };

        // Act
        var initializeResponse = await McpJsonRpcClient.SendLegacyInitializeAsync(
            client,
            "legacy-tool-call-initialize",
            patToken);
        var modernIdentityResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            identityParams,
            "modern-identity",
            patToken);
        var legacyIdentityResponse = await McpJsonRpcClient.SendLegacyRequestAsync(
            client,
            "tools/call",
            identityParams,
            "legacy-identity",
            patToken);
        var modernErrorResponse = await McpJsonRpcClient.SendRequestAsync(
            client,
            "tools/call",
            unknownToolParams,
            "modern-unknown-tool",
            patToken);
        var legacyErrorResponse = await McpJsonRpcClient.SendLegacyRequestAsync(
            client,
            "tools/call",
            unknownToolParams,
            "legacy-unknown-tool",
            patToken);
        using var modernIdentityPayload = await McpJsonRpcClient.ParseJsonAsync(modernIdentityResponse);
        using var legacyIdentityPayload = await McpJsonRpcClient.ParseJsonAsync(legacyIdentityResponse);
        using var modernErrorPayload = await McpJsonRpcClient.ParseJsonAsync(modernErrorResponse);
        using var legacyErrorPayload = await McpJsonRpcClient.ParseJsonAsync(legacyErrorResponse);

        // Assert
        Assert.Equal(HttpStatusCode.OK, initializeResponse.StatusCode);
        Assert.False(initializeResponse.Headers.Contains("Mcp-Session-Id"));
        Assert.Equal(HttpStatusCode.OK, modernIdentityResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, legacyIdentityResponse.StatusCode);
        Assert.Equal(
            McpJsonRpcClient.GetStructuredContent(modernIdentityPayload).GetRawText(),
            McpJsonRpcClient.GetStructuredContent(legacyIdentityPayload).GetRawText());
        Assert.Equal(HttpStatusCode.OK, modernErrorResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, legacyErrorResponse.StatusCode);
        var modernErrorResult = modernErrorPayload.RootElement.GetProperty("result");
        var legacyErrorResult = legacyErrorPayload.RootElement.GetProperty("result");
        Assert.True(modernErrorResult.GetProperty("isError").GetBoolean());
        Assert.True(legacyErrorResult.GetProperty("isError").GetBoolean());
        Assert.Equal(
            modernErrorResult.GetProperty("content").GetRawText(),
            legacyErrorResult.GetProperty("content").GetRawText());
    }

    [Theory]
    [InlineData("tools/list", false, true, "tools/list")]
    [InlineData("tools/call", true, false, "tools/call")]
    [InlineData("tools/list", true, true, "tools/call")]
    public async Task ModernRequest_WithInvalidRoutingHeaders_ShouldReturnHeaderMismatch(
        string method,
        bool includeRoutingMethod,
        bool includeRoutingName,
        string routingMethod)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);
        var requestParams = string.Equals(method, "tools/call", StringComparison.Ordinal)
            ? new { name = "identity_get", arguments = new { } }
            : (object)new { };

        // Act
        var response = await McpJsonRpcClient.SendModernRequestAsync(
            client,
            method,
            requestParams,
            "invalid-routing-header",
            patToken,
            routingMethod: routingMethod,
            includeRoutingMethod: includeRoutingMethod,
            includeRoutingName: includeRoutingName);
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(-32020, payload.RootElement.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task Request_WithUnsupportedModernProtocol_ShouldReturnSupportedVersions()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);

        // Act
        var response = await McpJsonRpcClient.SendModernRequestAsync(
            client,
            "tools/list",
            new { },
            "unsupported-protocol",
            patToken,
            protocolVersionHeader: "2099-01-01",
            protocolVersionMetadata: "2099-01-01");
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = payload.RootElement.GetProperty("error");
        Assert.Equal(-32022, error.GetProperty("code").GetInt32());
        Assert.Contains(
            McpJsonRpcClient.ModernProtocolVersion,
            error.GetProperty("data").GetProperty("supported").EnumerateArray().Select(version => version.GetString()));
    }
}
