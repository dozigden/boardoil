using BoardOil.Contracts.Contracts;
using Microsoft.AspNetCore.Http;

namespace BoardOil.Api.Mcp;

public sealed class McpErrorResponseFactory : IMcpErrorResponseFactory
{
    public ApiResult<object> CreateAuthError(string? mcpPublicBaseUrl, string detail) =>
        new(
            false,
            new
            {
                auth = McpDiscoveryMetadata.CreateAuthMetadata(mcpPublicBaseUrl),
                endpoint = McpDiscoveryMetadata.GetMcpEndpoint(mcpPublicBaseUrl),
                docs = McpDiscoveryMetadata.GetMcpDocsEndpoint(mcpPublicBaseUrl),
                setup = McpDiscoveryMetadata.CreateSetupMetadata(mcpPublicBaseUrl),
                examples = McpDiscoveryMetadata.CreateExamples(mcpPublicBaseUrl),
                nextStep = "Create a PAT in the machine access UI, then call POST /mcp with Authorization: Bearer <YOUR_PAT>."
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
                setup = McpDiscoveryMetadata.CreateSetupMetadata(mcpPublicBaseUrl),
                examples = McpDiscoveryMetadata.CreateExamples(mcpPublicBaseUrl),
                nextStep = "Use POST /mcp with a PAT bearer token. Legacy SSE-style paths are not supported."
            },
            404,
            "Unsupported MCP endpoint path.");
}
