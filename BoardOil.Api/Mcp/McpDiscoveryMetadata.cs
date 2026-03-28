namespace BoardOil.Api.Mcp;

public static class McpDiscoveryMetadata
{
    public static object CreateWellKnownDocument(string? mcpPublicBaseUrl) =>
        new
        {
            name = "BoardOil MCP",
            endpoint = GetMcpEndpoint(mcpPublicBaseUrl),
            protocol = "mcp-http",
            auth = CreateAuthMetadata(mcpPublicBaseUrl),
            setup = CreateSetupMetadata(mcpPublicBaseUrl),
            examples = CreateExamples(mcpPublicBaseUrl)
        };

    public static object CreateAuthMetadata(string? mcpPublicBaseUrl) =>
        new
        {
            scheme = "Bearer",
            headerName = "Authorization",
            tokenType = "personal_access_token",
            tokenPrefix = "bo_pat_",
            format = "Bearer <YOUR_PAT>",
            patManagementUi = ResolveUrl("/machine-access", mcpPublicBaseUrl)
        };

    public static object CreateSetupMetadata(string? mcpPublicBaseUrl) =>
        new
        {
            preferredAuth = "personal_access_token",
            patManagementUi = ResolveUrl("/machine-access", mcpPublicBaseUrl),
            examples = CreateExamples(mcpPublicBaseUrl)
        };

    public static object CreateExamples(string? mcpPublicBaseUrl) =>
        new
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
            }
        };

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
