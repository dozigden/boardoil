using BoardOil.Api.Configuration;
using BoardOil.Contracts.Common;
using Microsoft.AspNetCore.Http;

namespace BoardOil.Api.Mcp;

public sealed class McpErrorResponseFactory(BoardOilMcpOptions mcpOptions) : IMcpErrorResponseFactory
{
    private readonly BoardOilMcpOptions _mcpOptions = mcpOptions;

    public ApiResult<object> CreateAuthError(string? mcpPublicBaseUrl, string detail) =>
        new(
            false,
            new
            {
                auth = McpDiscoveryMetadata.CreateAuthMetadata(mcpPublicBaseUrl, _mcpOptions),
                endpoint = McpDiscoveryMetadata.GetMcpEndpoint(mcpPublicBaseUrl),
                docs = McpDiscoveryMetadata.GetMcpDocsEndpoint(mcpPublicBaseUrl),
                setup = McpDiscoveryMetadata.CreateSetupMetadata(mcpPublicBaseUrl, _mcpOptions),
                examples = McpDiscoveryMetadata.CreateExamples(mcpPublicBaseUrl, _mcpOptions),
                nextStep = _mcpOptions.AuthMode is McpAuthMode.None
                    ? "Call POST /mcp directly. Authentication is disabled for MCP in this environment."
                    : "Create an access token in the access tokens UI, then call POST /mcp with Authorization: Bearer <YOUR_ACCESS_TOKEN>."
            },
            401,
            detail);

    public ApiResult<object> CreateUnsupportedMcpPathError(PathString path, string? mcpPublicBaseUrl) =>
        new(
            false,
            new
            {
                requestedPath = path.ToString(),
                endpoint = McpDiscoveryMetadata.GetMcpEndpoint(mcpPublicBaseUrl),
                docs = McpDiscoveryMetadata.GetMcpDocsEndpoint(mcpPublicBaseUrl),
                setup = McpDiscoveryMetadata.CreateSetupMetadata(mcpPublicBaseUrl, _mcpOptions),
                examples = McpDiscoveryMetadata.CreateExamples(mcpPublicBaseUrl, _mcpOptions),
                nextStep = _mcpOptions.AuthMode is McpAuthMode.None
                    ? "Use POST /mcp. This server is configured for MCP without authentication."
                    : "Use POST /mcp with an access token as the bearer token. Legacy SSE-style paths are not supported."
            },
            404,
            "Unsupported MCP endpoint path.");
}
