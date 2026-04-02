using BoardOil.Api.Mcp;
using BoardOil.Mcp.Contracts;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpToolRegistryTests
{
    [Fact]
    public void Constructor_ShouldExposeRegisteredToolDefinitionsAndLookup()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<RegistryToolOne>();
        services.AddScoped<IMcpTool>(serviceProvider => serviceProvider.GetRequiredService<RegistryToolOne>());
        services.AddScoped<RegistryToolTwo>();
        services.AddScoped<IMcpTool>(serviceProvider => serviceProvider.GetRequiredService<RegistryToolTwo>());
        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var registry = new McpToolRegistry(serviceProvider);

        // Assert
        Assert.Equal(2, registry.Definitions.Count);
        Assert.True(registry.TryGetRegistration("test.one", out var registration));
        Assert.Equal(typeof(RegistryToolOne), registration.ImplementationType);
        Assert.False(registry.TryGetRegistration("missing.tool", out _));
    }

    [Fact]
    public void Constructor_WhenDuplicateToolNames_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<RegistryToolOne>();
        services.AddScoped<IMcpTool>(serviceProvider => serviceProvider.GetRequiredService<RegistryToolOne>());
        services.AddScoped<RegistryToolOneDuplicate>();
        services.AddScoped<IMcpTool>(serviceProvider => serviceProvider.GetRequiredService<RegistryToolOneDuplicate>());
        using var serviceProvider = services.BuildServiceProvider();

        // Act + Assert
        Assert.Throws<InvalidOperationException>(() => new McpToolRegistry(serviceProvider));
    }

    private sealed class RegistryToolOne : IMcpTool
    {
        public McpToolDefinition Definition { get; } = new("test.one", "tool one", "{}", "{}");

        public Task<CallToolResult> ExecuteAsync(McpInvocationContext context, IDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RegistryToolTwo : IMcpTool
    {
        public McpToolDefinition Definition { get; } = new("test.two", "tool two", "{}", "{}");

        public Task<CallToolResult> ExecuteAsync(McpInvocationContext context, IDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RegistryToolOneDuplicate : IMcpTool
    {
        public McpToolDefinition Definition { get; } = new("test.one", "duplicate tool one", "{}", "{}");

        public Task<CallToolResult> ExecuteAsync(McpInvocationContext context, IDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
