using BoardOil.Api.Configuration;
using BoardOil.Mcp.Contracts;

namespace BoardOil.Api.Mcp;

public static class McpDiscoveryMetadata
{
    public static object CreateWellKnownDocument(string? mcpPublicBaseUrl, BoardOilMcpOptions mcpOptions) =>
        new
        {
            name = "BoardOil MCP",
            endpoint = GetMcpEndpoint(mcpPublicBaseUrl),
            protocol = "mcp-http",
            auth = CreateAuthMetadata(mcpPublicBaseUrl, mcpOptions),
            setup = CreateSetupMetadata(mcpPublicBaseUrl, mcpOptions),
            profile = CreateProfileMetadata(),
            examples = CreateExamples(mcpPublicBaseUrl, mcpOptions)
        };

    public static object CreateAuthMetadata(string? mcpPublicBaseUrl, BoardOilMcpOptions mcpOptions) =>
        mcpOptions.AuthMode is McpAuthMode.None
            ? new
            {
                scheme = "None",
                required = false,
                mode = "no_auth",
                note = "No authentication required. Intended for trusted local or home-lab MCP clients."
            }
            : new
        {
            scheme = "Bearer",
            headerName = "Authorization",
            methods = new[] { "oauth", "personal_access_token" },
            oauth = new
            {
                resource = GetMcpEndpoint(mcpPublicBaseUrl),
                protectedResourceMetadata = GetMcpOAuthMetadataEndpoint(mcpPublicBaseUrl)
            },
            personalAccessToken = new
            {
                tokenPrefix = "bo_pat_",
                format = "Bearer <YOUR_PAT>",
                managementUi = ResolveUrl("/access-tokens", mcpPublicBaseUrl)
            }
        };

    public static object CreateSetupMetadata(string? mcpPublicBaseUrl, BoardOilMcpOptions mcpOptions) =>
        mcpOptions.AuthMode is McpAuthMode.None
            ? new
            {
                preferredAuth = "none",
                recommendedFirstCallSequence = CreateRecommendedFirstCallSequence()
            }
            : new
        {
            preferredAuth = "oauth",
            oauthProtectedResourceMetadata = GetMcpOAuthMetadataEndpoint(mcpPublicBaseUrl),
            manualFallbackAuth = "personal_access_token",
            patManagementUi = ResolveUrl("/access-tokens", mcpPublicBaseUrl),
            recommendedFirstCallSequence = CreateRecommendedFirstCallSequence()
        };

    public static object CreateProfileMetadata() =>
        new
        {
            mode = "tool-first",
            promptsList = "supported-empty-list",
            resourcesList = "supported-empty-list"
        };

    public static object CreateExamples(string? mcpPublicBaseUrl, BoardOilMcpOptions mcpOptions)
    {
        var endpoint = GetMcpEndpoint(mcpPublicBaseUrl);
        var bearerToken = mcpOptions.AuthMode is McpAuthMode.None
            ? null
            : "Bearer <YOUR_PAT>";

        return new
        {
            genericMcpConfig = CreateGenericMcpConfig(endpoint, bearerToken),
            serverDiscoverRequest = CreateRequestExample(
                endpoint,
                bearerToken,
                "server-discover",
                "server/discover",
                []),
            toolsListRequest = CreateRequestExample(
                endpoint,
                bearerToken,
                "tools-list",
                "tools/list",
                []),
            identityGetRequest = CreateRequestExample(
                endpoint,
                bearerToken,
                "identity-get",
                "tools/call",
                new Dictionary<string, object?>
                {
                    ["name"] = ToolNames.IdentityGet,
                    ["arguments"] = new { }
                }),
            boardListRequest = CreateRequestExample(
                endpoint,
                bearerToken,
                "board-list",
                "tools/call",
                new Dictionary<string, object?>
                {
                    ["name"] = ToolNames.BoardList,
                    ["arguments"] = new { }
                }),
            cardOptionsGetRequest = CreateRequestExample(
                endpoint,
                bearerToken,
                "card-options-get",
                "tools/call",
                new Dictionary<string, object?>
                {
                    ["name"] = ToolNames.CardOptionsGet,
                    ["arguments"] = new { id = 1 }
                })
        };
    }

    public static object[] CreateRecommendedFirstCallSequence() =>
    [
        new
        {
            step = 1,
            method = "server/discover",
            purpose = "Discover supported protocol versions and server capabilities without creating a session."
        },
        new
        {
            step = 2,
            method = "tools/list",
            purpose = "Discover available tools and argument schemas."
        },
        new
        {
            step = 3,
            method = "tools/call",
            tool = ToolNames.IdentityGet,
            purpose = "Identify the BoardOil user and authentication context when you need to confirm the current identity."
        },
        new
        {
            step = 4,
            method = "tools/call",
            tool = ToolNames.BoardList,
            purpose = "Discover accessible board ids when the target board is not already known."
        },
        new
        {
            step = 5,
            method = "tools/call",
            tool = ToolNames.CardOptionsGet,
            purpose = "Discover board-scoped values when setting controlled card fields."
        },
        new
        {
            step = 6,
            method = "tools/call",
            tool = ToolNames.BoardGet,
            purpose = "Fetch a board snapshot when current board state is needed."
        }
    ];

    public static string GetMcpEndpoint(string? mcpPublicBaseUrl) =>
        ResolveUrl("/mcp", mcpPublicBaseUrl);

    public static string GetMcpDocsEndpoint(string? mcpPublicBaseUrl) =>
        ResolveUrl("/.well-known/mcp", mcpPublicBaseUrl);

    public static string GetMcpOAuthMetadataEndpoint(string? mcpPublicBaseUrl) =>
        ResolveUrl("/.well-known/oauth-protected-resource/mcp", mcpPublicBaseUrl);

    private static Dictionary<string, object?> CreateGenericMcpConfig(
        string endpoint,
        string? bearerToken)
    {
        var config = new Dictionary<string, object?>
        {
            ["transport"] = "http",
            ["url"] = endpoint
        };
        if (bearerToken is not null)
        {
            config["headers"] = new Dictionary<string, string>
            {
                ["Authorization"] = bearerToken
            };
        }

        return config;
    }

    private static object CreateRequestExample(
        string endpoint,
        string? bearerToken,
        string id,
        string method,
        Dictionary<string, object?> requestParams)
    {
        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["MCP-Protocol-Version"] = "2026-07-28",
            ["Mcp-Method"] = method
        };
        if (bearerToken is not null)
        {
            headers["Authorization"] = bearerToken;
        }

        if (requestParams.TryGetValue("name", out var name) && name is string requestName)
        {
            headers["Mcp-Name"] = requestName;
        }

        requestParams["_meta"] = new Dictionary<string, object?>
        {
            ["io.modelcontextprotocol/protocolVersion"] = "2026-07-28",
            ["io.modelcontextprotocol/clientInfo"] = new
            {
                name = "BoardOil setup example",
                version = "1.0.0"
            },
            ["io.modelcontextprotocol/clientCapabilities"] = new { }
        };

        return new
        {
            method = "POST",
            url = endpoint,
            headers,
            body = new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = method,
                ["params"] = requestParams
            }
        };
    }

    private static string ResolveUrl(string path, string? mcpPublicBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(mcpPublicBaseUrl))
        {
            return path;
        }

        return $"{mcpPublicBaseUrl.TrimEnd('/')}{path}";
    }
}
