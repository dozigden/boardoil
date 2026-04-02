using BoardOil.Api.Mcp;
using BoardOil.Mcp.Contracts;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Text.Json;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpToolBaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenArgumentsAreInvalid_ShouldReturnValidationError()
    {
        // Arrange
        var tool = new TestTool(_ => throw new NotSupportedException());
        var context = CreateContext();
        var arguments = new Dictionary<string, JsonElement>
        {
            ["value"] = JsonSerializer.SerializeToElement("not-an-int")
        };

        // Act
        var result = await tool.ExecuteAsync(context, arguments, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        var payload = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("validation_failed", payload.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCoreThrows_ShouldReturnServiceError()
    {
        // Arrange
        var tool = new TestTool(_ => throw new InvalidOperationException("boom"));
        var context = CreateContext();
        var arguments = new Dictionary<string, JsonElement>
        {
            ["value"] = JsonSerializer.SerializeToElement(1)
        };

        // Act
        var result = await tool.ExecuteAsync(context, arguments, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        var payload = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("service_error", payload.GetProperty("code").GetString());
        Assert.Contains("boom", payload.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCoreReturnsFailure_ShouldReturnErrorPayload()
    {
        // Arrange
        var tool = new TestTool(_ =>
            Task.FromResult(new McpToolResult<TestOutput>(
                false,
                default,
                new McpToolError("forbidden", "Denied", 403))));
        var context = CreateContext();
        var arguments = new Dictionary<string, JsonElement>
        {
            ["value"] = JsonSerializer.SerializeToElement(1)
        };

        // Act
        var result = await tool.ExecuteAsync(context, arguments, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        var payload = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("forbidden", payload.GetProperty("code").GetString());
        Assert.Equal(403, payload.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCoreReturnsSuccess_ShouldReturnDirectStructuredPayload()
    {
        // Arrange
        var tool = new TestTool(input =>
            Task.FromResult(new McpToolResult<TestOutput>(
                true,
                new TestOutput(input.Value, "ok"),
                null)));
        var context = CreateContext();
        var arguments = new Dictionary<string, JsonElement>
        {
            ["value"] = JsonSerializer.SerializeToElement(7)
        };

        // Act
        var result = await tool.ExecuteAsync(context, arguments, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        var payload = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal(7, payload.GetProperty("value").GetInt32());
        Assert.Equal("ok", payload.GetProperty("message").GetString());
        Assert.False(payload.TryGetProperty("data", out _));
    }

    private static McpInvocationContext CreateContext()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        return new McpInvocationContext(serviceProvider, null, "test-correlation");
    }

    private sealed class TestTool(Func<TestInput, Task<McpToolResult<TestOutput>>> executor)
        : McpToolBase<TestInput, TestOutput>(new StubAuthorisationService())
    {
        private readonly Func<TestInput, Task<McpToolResult<TestOutput>>> _executor = executor;

        public override McpToolDefinition Definition { get; } = new("test.base", "test tool", "{}", "{}");

        protected override Task<McpToolResult<TestOutput>> ExecuteCoreAsync(McpInvocationContext context, TestInput input, CancellationToken cancellationToken) =>
            _executor(input);
    }

    private sealed class StubAuthorisationService : IMcpAuthorisationService
    {
        public PatAccessContext? GetPatAccessContext(ClaimsPrincipal? claimsPrincipal) => null;

        public McpToolError? EnsurePatToolAccess(PatAccessContext? patAccessContext, string requiredScope, int boardId) => null;
    }

    private sealed record TestInput
    {
        public int Value { get; init; }
    }

    private sealed record TestOutput(int Value, string Message);
}
