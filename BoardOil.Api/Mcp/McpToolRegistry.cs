using BoardOil.Mcp.Contracts;

using System.Diagnostics.CodeAnalysis;

namespace BoardOil.Api.Mcp;

public sealed class McpToolRegistry
{
    private readonly IReadOnlyDictionary<string, McpToolRegistration> _byName;
    private readonly IReadOnlyList<McpToolDefinition> _definitions;

    public McpToolRegistry(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var tools = scope.ServiceProvider.GetServices<IMcpTool>().ToArray();

        _byName = tools
            .GroupBy(tool => tool.Definition.Name, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    if (group.Count() > 1)
                    {
                        throw new InvalidOperationException($"Duplicate MCP tool registration for '{group.Key}'.");
                    }

                    var tool = group.Single();
                    return new McpToolRegistration(tool.Definition, tool.GetType());
                },
                StringComparer.Ordinal);

        _definitions = _byName.Values
            .Select(registration => registration.Definition)
            .OrderBy(definition => definition.Name, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<McpToolDefinition> Definitions => _definitions;

    public bool TryGetRegistration(string toolName, [NotNullWhen(true)] out McpToolRegistration? registration) =>
        _byName.TryGetValue(toolName, out registration);
}

public sealed record McpToolRegistration(
    McpToolDefinition Definition,
    Type ImplementationType);
