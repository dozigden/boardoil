using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Card;
using BoardOil.Ef;
using BoardOil.Mcp.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpCardSearchIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public McpCardSearchIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    [Theory]
    [InlineData(false, MachinePatScopes.McpRead)]
    [InlineData(true, MachinePatScopes.McpWrite)]
    public async Task Search_ShouldReturnOnlyDeclaredSummaryFieldsAndPagination(bool explicitPage, string scope)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, [scope]);
        var description = "Hidden CAFÉ match " + new string('x', 18000);
        var first = await CreateCardAsync(client, "First", description, [], null, null);
        var second = await CreateCardAsync(client, "Second", description, ["Context"], "Group", "https://example.com");
        object arguments = new { boardId = 1, query = "café" };
        if (explicitPage)
        {
            arguments = new { boardId = 1, query = "café", offset = 1, limit = 1 };
        }

        // Act
        using var payload = await CallAsync(client, token, arguments);

        // Assert
        var result = payload.RootElement.GetProperty("result");
        Assert.False(result.GetProperty("isError").GetBoolean());
        var output = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal(["cards", "totalCount", "offset", "limit"], output.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(2, output.GetProperty("totalCount").GetInt32());
        Assert.Equal(explicitPage ? 1 : 0, output.GetProperty("offset").GetInt32());
        Assert.Equal(explicitPage ? 1 : 20, output.GetProperty("limit").GetInt32());
        var expected = new[] { first, second }.OrderBy(card => card.SortKey, StringComparer.Ordinal).ToArray();
        if (explicitPage)
        {
            expected = expected.Skip(1).ToArray();
        }
        var cards = output.GetProperty("cards").EnumerateArray().ToArray();
        Assert.Equal(expected.Length, cards.Length);
        for (var index = 0; index < cards.Length; index++)
        {
            var card = cards[index];
            var source = expected[index];
            Assert.Equal(["id", "title", "columnId", "cardTypeId", "externalUrl", "tagNames", "slickName"],
                card.EnumerateObject().Select(p => p.Name).ToArray());
            Assert.Equal(source.Id, card.GetProperty("id").GetInt32());
            Assert.Equal(source.Title, card.GetProperty("title").GetString());
            Assert.Equal(source.BoardColumnId, card.GetProperty("columnId").GetInt32());
            Assert.Equal(source.CardTypeId, card.GetProperty("cardTypeId").GetInt32());
            Assert.Equal(source.ExternalUrl, card.GetProperty("externalUrl").GetString());
            Assert.Equal(source.SlickName, card.GetProperty("slickName").GetString());
            Assert.Equal(source.TagNames, card.GetProperty("tagNames").EnumerateArray().Select(tag => tag.GetString()).ToArray());
        }
        Assert.DoesNotContain(description, payload.RootElement.GetRawText(), StringComparison.Ordinal);
        var text = Assert.Single(result.GetProperty("content").EnumerateArray());
        using var textPayload = JsonDocument.Parse(text.GetProperty("text").GetString()!);
        Assert.True(JsonElement.DeepEquals(output, textPayload.RootElement));
    }

    [Theory]
    [InlineData("{\"query\":\"x\"}")]
    [InlineData("{\"boardId\":1}")]
    [InlineData("{\"boardId\":1,\"query\":\"  \"}")]
    [InlineData("{\"boardId\":1,\"query\":\"x\",\"limit\":0}")]
    [InlineData("{\"boardId\":1,\"query\":\"x\",\"offset\":null}")]
    [InlineData("{\"boardId\":1,\"query\":\"x\",\"limit\":true}")]
    [InlineData("{\"boardId\":1,\"query\":\"x\",\"tagNames\":[\"MCP\"]}")]
    public async Task Search_WithInvalidArguments_ShouldReturnValidationError(string arguments)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        using var input = JsonDocument.Parse(arguments);

        // Act
        using var payload = await CallAsync(client, token, input.RootElement);

        // Assert
        Assert.True(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var error = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal("validation_failed", error.GetProperty("code").GetString());
        Assert.Equal(400, error.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task Search_AfterBoardMembershipRemoval_ShouldReturnForbidden()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        using (var services = Factory.Services.CreateScope())
        {
            var factory = services.ServiceProvider.GetRequiredService<IDbContextFactory>();
            await using var db = factory.CreateDbContext<BoardOilDbContext>();
            var membership = await db.BoardMembers.SingleAsync(x => x.BoardId == 1 && x.UserId == 1);
            db.BoardMembers.Remove(membership);
            await db.SaveChangesAsync();
        }

        // Act
        using var payload = await CallAsync(client, token, new { boardId = 1, query = "secret" });

        // Assert
        Assert.True(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var error = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal("forbidden", error.GetProperty("code").GetString());
        Assert.Equal(403, error.GetProperty("statusCode").GetInt32());
    }

    private static async Task<CardDto> CreateCardAsync(
        HttpClient client, string title, string description, string[] tags, string? slick, string? externalUrl)
    {
        using var response = await client.PostAsJsonAsync("/api/boards/1/cards",
            new CreateCardRequest(null, title, description, tags, SlickName: slick, ExternalUrl: externalUrl));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>())!.Data!;
    }

    private static async Task<JsonDocument> CallAsync(HttpClient client, string token, object arguments)
    {
        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/call",
            new { name = ToolNames.CardSearch, arguments }, "card-search", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await McpJsonRpcClient.ParseJsonAsync(response);
    }
}
