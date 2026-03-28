namespace BoardOil.Api.Mcp;

public static class McpDiscoveryMetadata
{
    public static object CreateWellKnownDocument(string baseUrl) =>
        new
        {
            name = "BoardOil MCP",
            endpoint = $"{baseUrl}/mcp",
            protocol = "mcp-http",
            auth = CreateAuthMetadata(baseUrl),
            setup = CreateSetupMetadata(baseUrl)
        };

    public static object CreateAuthMetadata(string baseUrl) =>
        new
        {
            scheme = "Bearer",
            tokenEndpoint = $"{baseUrl}/api/auth/machine/login",
            refreshEndpoint = $"{baseUrl}/api/auth/machine/refresh"
        };

    public static object CreateSetupMetadata(string baseUrl) =>
        new
        {
            preferredAuth = "personal_access_token",
            patManagementUi = $"{baseUrl}/machine-access",
            examples = new
            {
                genericMcpConfig = new
                {
                    transport = "http",
                    url = $"{baseUrl}/mcp",
                    headers = new
                    {
                        Authorization = "Bearer <YOUR_PAT>"
                    }
                },
                toolsListRequest = new
                {
                    method = "POST",
                    url = $"{baseUrl}/mcp",
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
                }
            }
        };
}
