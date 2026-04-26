using System.Net;
using BoardOil.Api.Tests.Infrastructure;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpProtocolListHandlersIntegrationTests : McpIntegrationTestBase
{
    [Fact]
    public async Task PromptsList_WithValidPatBearerToken_ShouldReturnEmptyListResult()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);

        // Act
        var response = await McpJsonRpcClient.SendRequestAsync(client, "prompts/list", new { }, "prompts-list", patToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);
        var result = payload.RootElement.GetProperty("result");
        Assert.True(result.TryGetProperty("prompts", out var prompts));
        Assert.Empty(prompts.EnumerateArray());
    }

    [Fact]
    public async Task ResourcesList_WithValidPatBearerToken_ShouldReturnEmptyListResult()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var patToken = await CreateMachinePatAsync(client);

        // Act
        var response = await McpJsonRpcClient.SendRequestAsync(client, "resources/list", new { }, "resources-list", patToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);
        var result = payload.RootElement.GetProperty("result");
        Assert.True(result.TryGetProperty("resources", out var resources));
        Assert.Empty(resources.EnumerateArray());
    }
}
