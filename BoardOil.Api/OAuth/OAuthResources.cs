namespace BoardOil.Api.OAuth;

internal static class OAuthResources
{
    public const string McpType = "mcp";
    public const string McpPath = "/mcp";
    public const string McpMetadataPath = "/.well-known/oauth-protected-resource/mcp";
    public const string LegacyMcpPath = "/mcp/oauth";
    public const string LegacyMcpMetadataPath = "/.well-known/oauth-protected-resource/mcp/oauth";

    public static bool IsMcpPath(PathString path) =>
        path.StartsWithSegments(McpPath, StringComparison.OrdinalIgnoreCase);

    public static string ResolveResourcePath(PathString requestPath) =>
        requestPath.StartsWithSegments(LegacyMcpPath, StringComparison.OrdinalIgnoreCase)
            ? LegacyMcpPath
            : McpPath;

    public static string ResolveMetadataPath(PathString requestPath) =>
        requestPath.StartsWithSegments(LegacyMcpPath, StringComparison.OrdinalIgnoreCase)
            ? LegacyMcpMetadataPath
            : McpMetadataPath;
}
