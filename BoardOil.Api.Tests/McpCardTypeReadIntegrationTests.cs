using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.CardType;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using BoardOil.Mcp.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpCardTypeReadIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public McpCardTypeReadIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    [Fact]
    public async Task Read_AsContributorWithReadScope_ShouldReturnCompleteDefinitionsAndCurrentDefault()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:read"]);
        var definitions = new[]
        {
            new CreateCardTypeRequest("Automatic", null, "auto", "{}"),
            new CreateCardTypeRequest("Preset", "🐈", "presets", """{"presetIndex":11}"""),
            new CreateCardTypeRequest("Solid", "🐈", "solid", """{"backgroundColor":"#224466","textColorMode":"custom","textColor":"#FFFFFF","borderMode":"none"}"""),
            new CreateCardTypeRequest("Gradient", "🐈", "gradient", """{"leftColor":"#224466","rightColor":"#6688AA","textColorMode":"auto","borderMode":"custom","borderColor":"#123456"}""")
        };
        var created = new List<CardTypeDto>();
        foreach (var definition in definitions)
        {
            using var response = await client.PostAsJsonAsync("/api/boards/1/card-types", definition);
            response.EnsureSuccessStatusCode();
            created.Add((await response.Content.ReadFromJsonAsync<ApiEnvelope<CardTypeDto>>())!.Data!);
        }
        using var defaultResponse = await client.PatchAsync($"/api/boards/1/card-types/{created[1].Id}/default", null);
        defaultResponse.EnsureSuccessStatusCode();
        await using (var db = CreateDbContext())
        {
            var membership = await db.BoardMembers.SingleAsync(member => member.BoardId == 1 && member.UserId == 1);
            membership.Role = BoardMemberRole.Contributor;
            await db.SaveChangesAsync();
        }

        // Act
        using var payload = await CallAsync(client, token);

        // Assert
        Assert.False(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var output = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal(created[1].Id, output.GetProperty("defaultCardTypeId").GetInt32());
        var cardTypes = output.GetProperty("cardTypes").EnumerateArray().ToArray();
        for (var index = 0; index < created.Count; index++)
        {
            var source = created[index];
            var cardType = cardTypes.Single(type => type.GetProperty("id").GetInt32() == source.Id);
            Assert.Equal(["id", "name", "emoji", "style", "createdAtUtc", "updatedAtUtc"],
                cardType.EnumerateObject().Select(property => property.Name));
            Assert.Equal(source.Name, cardType.GetProperty("name").GetString());
            Assert.Equal(source.Emoji, cardType.GetProperty("emoji").GetString());
            Assert.Equal(source.CreatedAtUtc, cardType.GetProperty("createdAtUtc").GetDateTime());
            Assert.True(cardType.GetProperty("updatedAtUtc").GetDateTime() >= source.UpdatedAtUtc);
            var style = cardType.GetProperty("style");
            Assert.Equal(source.StyleName, style.GetProperty("styleName").GetString());
            using var properties = JsonDocument.Parse(source.StylePropertiesJson);
            Assert.Equal(properties.RootElement.EnumerateObject().Count() + 1, style.EnumerateObject().Count());
            foreach (var property in properties.RootElement.EnumerateObject())
            {
                Assert.True(JsonElement.DeepEquals(property.Value, style.GetProperty(property.Name)));
            }
            var typedStyle = style.Deserialize<McpStyle>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.True(JsonElement.DeepEquals(style, JsonSerializer.SerializeToElement(typedStyle, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
        }
    }

    [Theory]
    [InlineData("unknown", "{}")]
    [InlineData("solid", "{\"backgroundColor\":\"#224466\",\"textColorMode\":\"auto\"}")]
    public async Task Read_WithInvalidStoredStyle_ShouldFailWithoutReturningPartialDefinitions(string styleName, string propertiesJson)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:read"]);
        var invalidType = new EntityCardType
        {
            BoardId = 1,
            Name = "Invalid type",
            StyleName = styleName,
            StylePropertiesJson = propertiesJson
        };
        await using (var db = CreateDbContext())
        {
            db.CardTypes.Add(invalidType);
            await db.SaveChangesAsync();
        }

        // Act
        using var payload = await CallAsync(client, token);

        // Assert
        var error = AssertError(payload, "data_integrity_error", 500);
        Assert.Contains($"Card type {invalidType.Id} ('Invalid type')", error.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.False(error.TryGetProperty("cardTypes", out _));
    }

    [Fact]
    public async Task Read_AfterMembershipRemoval_ShouldReturnForbidden()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:read"]);
        await using (var db = CreateDbContext())
        {
            var membership = await db.BoardMembers.SingleAsync(member => member.BoardId == 1 && member.UserId == 1);
            db.BoardMembers.Remove(membership);
            await db.SaveChangesAsync();
        }

        // Act
        using var payload = await CallAsync(client, token);

        // Assert
        AssertError(payload, "forbidden", 403);
    }

    private BoardOilDbContext CreateDbContext() => Factory.Services.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();

    private static async Task<JsonDocument> CallAsync(HttpClient client, string token)
    {
        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/call",
            new { name = ToolNames.CardOptionsGet, arguments = new { id = 1 } }, "card-type-read", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await McpJsonRpcClient.ParseJsonAsync(response);
    }

    private static JsonElement AssertError(JsonDocument payload, string code, int statusCode)
    {
        Assert.True(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var error = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.Equal(statusCode, error.GetProperty("statusCode").GetInt32());
        return error;
    }
}
