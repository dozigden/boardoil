using BoardOil.Contracts.Common;
using Microsoft.AspNetCore.Http;

namespace BoardOil.Api.Mcp;

public interface IMcpErrorResponseFactory
{
    ApiResult<object> CreateAuthError(string? mcpPublicBaseUrl, string detail);

    ApiResult<object> CreateUnsupportedMcpPathError(PathString path, string? mcpPublicBaseUrl);
}
