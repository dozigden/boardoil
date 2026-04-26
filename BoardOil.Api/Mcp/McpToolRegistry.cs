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

        var canonicalByName = tools
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

        var byName = new Dictionary<string, McpToolRegistration>(canonicalByName, StringComparer.Ordinal);
        foreach (var pair in canonicalByName)
        {
            var legacyAlias = pair.Key.Replace("_", ".", StringComparison.Ordinal);
            if (string.Equals(legacyAlias, pair.Key, StringComparison.Ordinal))
            {
                continue;
            }

            byName.TryAdd(legacyAlias, pair.Value);
        }

        _byName = byName;

        _definitions = canonicalByName.Values
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
