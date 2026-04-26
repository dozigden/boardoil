using BoardOil.Api.Configuration;
using BoardOil.Api.Mcp;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpErrorResponseFactoryTests
{
    private readonly McpErrorResponseFactory _factory = new(new BoardOilMcpOptions());

    [Fact]
    public void CreateAuthError_ShouldReturnConsistentPayload()
    {
        // Arrange
        const string baseUrl = "https://boardoil.example.com/base";

        // Act
        var result = _factory.CreateAuthError(baseUrl, "Invalid or expired bearer token.");

        // Assert
        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
        Assert.Contains("Invalid or expired bearer token", result.Message, StringComparison.OrdinalIgnoreCase);

        Assert.NotNull(result.Data);
        var payload = result.Data!.ToJsonElement();
        Assert.Equal("https://boardoil.example.com/base/mcp", payload.GetProperty("endpoint").GetString());
        Assert.Equal("Bearer", payload.GetProperty("auth").GetProperty("scheme").GetString());
        Assert.Equal("POST", payload.GetProperty("examples").GetProperty("toolsListRequest").GetProperty("method").GetString());
    }

    [Fact]
    public void CreateUnsupportedMcpPathError_ShouldIncludeRequestedPathAndDocs()
    {
        // Arrange
        var path = new PathString("/v1/mcp");

        // Act
        var result = _factory.CreateUnsupportedMcpPathError(path, null);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        var payload = result.Data!.ToJsonElement();
        Assert.Equal("/v1/mcp", payload.GetProperty("requestedPath").GetString());
        Assert.Equal("/mcp", payload.GetProperty("endpoint").GetString());
        Assert.Contains("PAT bearer token", payload.GetProperty("nextStep").GetString(), StringComparison.OrdinalIgnoreCase);
    }
}

internal static class McpErrorResponseFactoryTestsJsonExtensions
{
    public static System.Text.Json.JsonElement ToJsonElement(this object value) =>
        System.Text.Json.JsonSerializer.SerializeToElement(value);
}
