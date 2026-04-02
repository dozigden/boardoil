using BoardOil.Mcp.Contracts;
using ModelContextProtocol.Protocol;
using System.Text.Json;

namespace BoardOil.Api.Mcp;

public interface IMcpTool
{
    McpToolDefinition Definition { get; }

    Task<CallToolResult> ExecuteAsync(
        McpInvocationContext context,
        IDictionary<string, JsonElement>? arguments,
        CancellationToken cancellationToken);
}
