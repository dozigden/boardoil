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
            tokenType = "personal_access_token",
            tokenPrefix = "bo_pat_",
            format = "Bearer <YOUR_PAT>",
            patManagementUi = ResolveUrl("/access-tokens", mcpPublicBaseUrl)
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
            preferredAuth = "personal_access_token",
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

    public static object CreateExamples(string? mcpPublicBaseUrl, BoardOilMcpOptions mcpOptions) =>
        mcpOptions.AuthMode is McpAuthMode.None
            ? new
            {
                genericMcpConfig = new
                {
                    transport = "http",
                    url = GetMcpEndpoint(mcpPublicBaseUrl)
                },
                toolsListRequest = new
                {
                    method = "POST",
                    url = GetMcpEndpoint(mcpPublicBaseUrl),
                    headers = new
                    {
                        contentType = "application/json"
                    },
                    body = new
                    {
                        jsonrpc = "2.0",
                        id = "tools-list",
                        method = "tools/list"
                    }
                },
                identityGetRequest = new
                {
                    method = "POST",
                    url = GetMcpEndpoint(mcpPublicBaseUrl),
                    headers = new
                    {
                        contentType = "application/json"
                    },
                    body = new
                    {
                        jsonrpc = "2.0",
                        id = "identity-get",
                        method = "tools/call",
                        @params = new
                        {
                            name = ToolNames.IdentityGet,
                            arguments = new { }
                        }
                    }
                },
                boardListRequest = new
                {
                    method = "POST",
                    url = GetMcpEndpoint(mcpPublicBaseUrl),
                    headers = new
                    {
                        contentType = "application/json"
                    },
                    body = new
                    {
                        jsonrpc = "2.0",
                        id = "board-list",
                        method = "tools/call",
                        @params = new
                        {
                            name = ToolNames.BoardList,
                            arguments = new { }
                        }
                    }
                },
                cardOptionsGetRequest = new
                {
                    method = "POST",
                    url = GetMcpEndpoint(mcpPublicBaseUrl),
                    headers = new
                    {
                        contentType = "application/json"
                    },
                    body = new
                    {
                        jsonrpc = "2.0",
                        id = "card-options-get",
                        method = "tools/call",
                        @params = new
                        {
                            name = ToolNames.CardOptionsGet,
                            arguments = new { id = 1 }
                        }
                    }
                }
            }
            : new
        {
            genericMcpConfig = new
            {
                transport = "http",
                url = GetMcpEndpoint(mcpPublicBaseUrl),
                headers = new
                {
                    Authorization = "Bearer <YOUR_PAT>"
                }
            },
            toolsListRequest = new
            {
                method = "POST",
                url = GetMcpEndpoint(mcpPublicBaseUrl),
                headers = new
                {
                    Authorization = "Bearer <YOUR_PAT>",
                    contentType = "application/json"
                },
                body = new
                {
                    jsonrpc = "2.0",
                    id = "tools-list",
                    method = "tools/list"
                }
            },
            identityGetRequest = new
            {
                method = "POST",
                url = GetMcpEndpoint(mcpPublicBaseUrl),
                headers = new
                {
                    Authorization = "Bearer <YOUR_PAT>",
                    contentType = "application/json"
                },
                body = new
                {
                    jsonrpc = "2.0",
                    id = "identity-get",
                    method = "tools/call",
                    @params = new
                    {
                        name = ToolNames.IdentityGet,
                        arguments = new { }
                    }
                }
            },
            boardListRequest = new
            {
                method = "POST",
                url = GetMcpEndpoint(mcpPublicBaseUrl),
                headers = new
                {
                    Authorization = "Bearer <YOUR_PAT>",
                    contentType = "application/json"
                },
                body = new
                {
                    jsonrpc = "2.0",
                    id = "board-list",
                    method = "tools/call",
                    @params = new
                    {
                        name = ToolNames.BoardList,
                        arguments = new { }
                    }
                }
            },
            cardOptionsGetRequest = new
            {
                method = "POST",
                url = GetMcpEndpoint(mcpPublicBaseUrl),
                headers = new
                {
                    Authorization = "Bearer <YOUR_PAT>",
                    contentType = "application/json"
                },
                body = new
                {
                    jsonrpc = "2.0",
                    id = "card-options-get",
                    method = "tools/call",
                    @params = new
                    {
                        name = ToolNames.CardOptionsGet,
                        arguments = new { id = 1 }
                    }
                }
            }
        };

    public static object[] CreateRecommendedFirstCallSequence() =>
    [
        new
        {
            step = 1,
            method = "tools/list",
            purpose = "Discover available tools and argument schemas."
        },
        new
        {
            step = 2,
            method = "tools/call",
            tool = ToolNames.IdentityGet,
            purpose = "Identify the BoardOil user and authentication context when you need to confirm the current identity."
        },
        new
        {
            step = 3,
            method = "tools/call",
            tool = ToolNames.BoardList,
            purpose = "Discover accessible board ids when the target board is not already known."
        },
        new
        {
            step = 4,
            method = "tools/call",
            tool = ToolNames.CardOptionsGet,
            purpose = "Discover board-scoped values when setting controlled card fields."
        },
        new
        {
            step = 5,
            method = "tools/call",
            tool = ToolNames.BoardGet,
            purpose = "Fetch a board snapshot when current board state is needed."
        }
    ];

    public static string GetMcpEndpoint(string? mcpPublicBaseUrl) =>
        ResolveUrl("/mcp", mcpPublicBaseUrl);

    public static string GetMcpDocsEndpoint(string? mcpPublicBaseUrl) =>
        ResolveUrl("/.well-known/mcp", mcpPublicBaseUrl);

    private static string ResolveUrl(string path, string? mcpPublicBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(mcpPublicBaseUrl))
        {
            return path;
        }

        return $"{mcpPublicBaseUrl.TrimEnd('/')}{path}";
    }
}
