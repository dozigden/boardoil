using BoardOil.Mcp.Contracts;

namespace BoardOil.Mcp.Server.Tools;

public sealed class ToolRegistry
{
    public IReadOnlyList<McpToolDefinition> ListTools() => ToolCatalogue.Definitions;
}
