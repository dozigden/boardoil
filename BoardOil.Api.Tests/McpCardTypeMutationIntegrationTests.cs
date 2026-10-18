using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.CardType;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using BoardOil.Mcp.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpCardTypeMutationIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public McpCardTypeMutationIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    public static TheoryData<string, string, string> StyleCases { get; } = new()
    {
        { "auto", "{}", """{"styleName":"auto"}""" },
        { "presets", """{"presetIndex":7}""", """{"styleName":"presets","presetIndex":7}""" },
        { "solid", """{"backgroundColor":"#224466","textColorMode":"custom","textColor":"#ffffff","borderMode":"none"}""", """{"styleName":"solid","backgroundColor":"#224466","textColorMode":"custom","textColor":"#FFFFFF","borderMode":"none"}""" },
        { "gradient", """{"leftColor":"#224466","rightColor":"#6688aa","textColorMode":"auto","borderMode":"custom","borderColor":"#123456"}""", """{"styleName":"gradient","leftColor":"#224466","rightColor":"#6688AA","textColorMode":"auto","borderMode":"custom","borderColor":"#123456"}""" }
    };

    [Theory]
    [MemberData(nameof(StyleCases))]
    public async Task Create_WithCompleteDefinition_ShouldReturnCanonicalPersistedType(string styleName, string propertiesJson, string expectedStyleJson)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:write"]);
        var arguments = DefinitionArguments(ToolNames.CardTypeCreate);
        arguments["name"] = "  MCP type  ";
        arguments["style"] = StyleArgument(styleName, propertiesJson);

        // Act
        using var payload = await CallAsync(client, token, ToolNames.CardTypeCreate, arguments);

        // Assert
        var output = AssertSuccess(payload);
        Assert.Equal("created", output.GetProperty("outcome").GetString());
        var snapshot = output.GetProperty("cardType");
        Assert.Equal("MCP type", snapshot.GetProperty("name").GetString());
        Assert.Equal("🐈", snapshot.GetProperty("emoji").GetString());
        var stored = (await ReadTypesAsync(client)).Single(type => type.Id == snapshot.GetProperty("id").GetInt32());
        Assert.False(stored.IsSystem);
        using var expectedStyle = JsonDocument.Parse(expectedStyleJson);
        Assert.True(JsonElement.DeepEquals(expectedStyle.RootElement, snapshot.GetProperty("style")));
        AssertDefinition(snapshot, stored);
    }

    [Theory]
    [MemberData(nameof(StyleCases))]
    public async Task Update_WithReplacementStyle_ShouldPersistRequestedStyle(string styleName, string propertiesJson, string expectedStyleJson)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:write"]);
        var type = await CreateTypeAsync(client, "presets", """{"presetIndex":2}""");
        var arguments = new
        {
            boardId = 1,
            id = type.Id,
            name = type.Name,
            emoji = type.Emoji,
            style = StyleArgument(styleName, propertiesJson)
        };

        // Act
        using var payload = await CallAsync(client, token, ToolNames.CardTypeUpdate, arguments);

        // Assert
        var output = AssertSuccess(payload);
        Assert.Equal("updated", output.GetProperty("outcome").GetString());
        var snapshot = output.GetProperty("cardType");
        Assert.Equal(type.Id, snapshot.GetProperty("id").GetInt32());
        Assert.Equal(type.Name, snapshot.GetProperty("name").GetString());
        Assert.Equal(type.Emoji, snapshot.GetProperty("emoji").GetString());
        using var expectedStyle = JsonDocument.Parse(expectedStyleJson);
        Assert.True(JsonElement.DeepEquals(expectedStyle.RootElement, snapshot.GetProperty("style")));
        AssertDefinition(snapshot, (await ReadTypesAsync(client)).Single(item => item.Id == type.Id));
    }

    [Theory]
    [InlineData("auto", "{}")]
    [InlineData("presets", "{\"presetIndex\":7}")]
    [InlineData("solid", "{\"backgroundColor\":\"#224466\",\"textColorMode\":\"custom\",\"textColor\":\"#FFFFFF\",\"borderMode\":\"none\"}")]
    [InlineData("gradient", "{\"leftColor\":\"#224466\",\"rightColor\":\"#6688AA\",\"textColorMode\":\"auto\",\"borderMode\":\"custom\",\"borderColor\":\"#123456\"}")]
    public async Task Update_WithCatalogueStyle_ShouldPreserveStyleAndClearEmoji(string styleName, string propertiesJson)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, ["mcp:write"]);
        var type = await CreateTypeAsync(client, styleName, propertiesJson);
        using var catalogue = await CallAsync(client, token, ToolNames.CardOptionsGet, new { id = 1 });
        var source = AssertSuccess(catalogue).GetProperty("cardTypes").EnumerateArray().Single(item => item.GetProperty("id").GetInt32() == type.Id);
        var arguments = new
        {
            boardId = 1,
            id = type.Id,
            name = "Renamed type",
            emoji = (string?)null,
            style = source.GetProperty("style")
        };

        // Act
        using var payload = await CallAsync(client, token, ToolNames.CardTypeUpdate, arguments);

        // Assert
        var output = AssertSuccess(payload);
        Assert.Equal("updated", output.GetProperty("outcome").GetString());
        var snapshot = output.GetProperty("cardType");
        Assert.Equal(type.Id, snapshot.GetProperty("id").GetInt32());
        Assert.Equal("Renamed type", snapshot.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, snapshot.GetProperty("emoji").ValueKind);
        Assert.True(JsonElement.DeepEquals(source.GetProperty("style"), snapshot.GetProperty("style")));
        AssertDefinition(snapshot, (await ReadTypesAsync(client)).Single(item => item.Id == type.Id));
    }

    [Fact]
    public async Task Create_WithDuplicateName_ShouldReturnServiceValidationWithoutChangingExistingType()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var existing = await CreateTypeAsync(client);
        var arguments = DefinitionArguments(ToolNames.CardTypeCreate);
        arguments["name"] = existing.Name.ToLowerInvariant();

        // Act
        using var payload = await CallAsync(client, token, ToolNames.CardTypeCreate, arguments);

        // Assert
        var error = AssertError(payload, "validation_failed", 400);
        Assert.True(error.GetProperty("validationErrors").TryGetProperty("name", out _));
        Assert.Equal(existing, (await ReadTypesAsync(client)).Single(item => item.Id == existing.Id));
    }

    [Theory]
    [InlineData(ToolNames.CardTypeCreate, "name")]
    [InlineData(ToolNames.CardTypeCreate, "emoji")]
    [InlineData(ToolNames.CardTypeCreate, "style")]
    [InlineData(ToolNames.CardTypeUpdate, "name")]
    [InlineData(ToolNames.CardTypeUpdate, "emoji")]
    [InlineData(ToolNames.CardTypeUpdate, "style")]
    [InlineData(ToolNames.CardTypeCreate, "boardId")]
    [InlineData(ToolNames.CardTypeUpdate, "id")]
    [InlineData(ToolNames.CardTypeSetDefault, "id")]
    [InlineData(ToolNames.CardTypeDelete, "boardId")]
    public async Task Mutation_WithMissingRequiredField_ShouldReturnFieldError(string toolName, string missingField)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var arguments = DefinitionArguments(toolName);
        arguments.Remove(missingField);

        // Act
        using var payload = await CallAsync(client, token, toolName, arguments);

        // Assert
        var error = AssertError(payload, "validation_failed", 400);
        Assert.True(error.GetProperty("validationErrors").TryGetProperty(missingField, out _));
    }

    [Theory]
    [InlineData(ToolNames.CardTypeCreate)]
    [InlineData(ToolNames.CardTypeUpdate)]
    public async Task Definition_WithInvalidStyle_ShouldReturnStructuredFieldError(string toolName)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var arguments = DefinitionArguments(toolName);
        arguments["style"] = StyleArgument("solid", """{"backgroundColor":"#224466","textColorMode":"custom","borderMode":"auto"}""");

        // Act
        using var payload = await CallAsync(client, token, toolName, arguments);

        // Assert
        var error = AssertError(payload, "validation_failed", 400);
        Assert.True(error.GetProperty("validationErrors").TryGetProperty("style.textColor", out _));
    }

    [Theory]
    [InlineData(ToolNames.CardTypeCreate, false)]
    [InlineData(ToolNames.CardTypeUpdate, false)]
    [InlineData(ToolNames.CardTypeSetDefault, false)]
    [InlineData(ToolNames.CardTypeDelete, false)]
    [InlineData(ToolNames.CardTypeCreate, true)]
    [InlineData(ToolNames.CardTypeUpdate, true)]
    [InlineData(ToolNames.CardTypeSetDefault, true)]
    [InlineData(ToolNames.CardTypeDelete, true)]
    public async Task Mutation_WithoutWriteScopeOrSettingsPermission_ShouldReturnForbidden(string toolName, bool contributor)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client, [contributor ? "mcp:write" : "mcp:read"]);
        if (contributor)
        {
            await using var db = Factory.Services.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>();
            var membership = await db.BoardMembers.SingleAsync(member => member.BoardId == 1 && member.UserId == 1);
            membership.Role = BoardMemberRole.Contributor;
            await db.SaveChangesAsync();
        }

        // Act
        using var payload = await CallAsync(client, token, toolName, DefinitionArguments(toolName));

        // Assert
        AssertError(payload, "forbidden", 403);
    }

    [Theory]
    [InlineData(ToolNames.CardTypeUpdate)]
    [InlineData(ToolNames.CardTypeSetDefault)]
    [InlineData(ToolNames.CardTypeDelete)]
    public async Task Mutation_WithTypeFromAnotherBoard_ShouldNotAffectThatType(string toolName)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        using var boardResponse = await client.PostAsJsonAsync("/api/boards", new CreateBoardRequest("Other board"));
        boardResponse.EnsureSuccessStatusCode();
        var otherBoard = (await boardResponse.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>())!.Data!;
        var otherType = Assert.Single(await ReadTypesAsync(client, otherBoard.Id));
        var arguments = DefinitionArguments(toolName);
        arguments["id"] = otherType.Id;

        // Act
        using var payload = await CallAsync(client, token, toolName, arguments);

        // Assert
        if (toolName == ToolNames.CardTypeDelete)
        {
            Assert.Equal("deleted", AssertSuccess(payload).GetProperty("outcome").GetString());
        }
        else
        {
            AssertError(payload, "not_found", 404);
        }
        Assert.Equal(otherType, Assert.Single(await ReadTypesAsync(client, otherBoard.Id)));
    }

    [Fact]
    public async Task SetDefault_ShouldSwitchDefaultWithoutReassigningExistingCards()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var originalDefault = (await ReadTypesAsync(client)).Single(type => type.IsSystem);
        var nextDefault = await CreateTypeAsync(client);
        var existingCard = await CreateCardAsync(client, originalDefault.Id);

        // Act
        using var payload = await CallAsync(client, token, ToolNames.CardTypeSetDefault, new { boardId = 1, id = nextDefault.Id });

        // Assert
        var output = AssertSuccess(payload);
        Assert.Equal(nextDefault.Id, output.GetProperty("id").GetInt32());
        Assert.Equal("default_set", output.GetProperty("outcome").GetString());
        Assert.Equal(nextDefault.Id, (await ReadTypesAsync(client)).Single(type => type.IsSystem).Id);
        using var storedCard = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id = existingCard.Id });
        Assert.Equal(originalDefault.Id, AssertSuccess(storedCard).GetProperty("cardTypeId").GetInt32());
        var newCard = await CreateCardAsync(client, null);
        Assert.Equal(nextDefault.Id, newCard.CardTypeId);
    }

    [Fact]
    public async Task Delete_ShouldReassignCardsToCurrentDefault()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var defaultType = (await ReadTypesAsync(client)).Single(type => type.IsSystem);
        var type = await CreateTypeAsync(client);
        var card = await CreateCardAsync(client, type.Id);

        // Act
        using var payload = await CallAsync(client, token, ToolNames.CardTypeDelete, new { boardId = 1, id = type.Id });

        // Assert
        var output = AssertSuccess(payload);
        Assert.Equal(type.Id, output.GetProperty("id").GetInt32());
        Assert.Equal("deleted", output.GetProperty("outcome").GetString());
        Assert.DoesNotContain(await ReadTypesAsync(client), item => item.Id == type.Id);
        using var storedCard = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id = card.Id });
        Assert.Equal(defaultType.Id, AssertSuccess(storedCard).GetProperty("cardTypeId").GetInt32());
    }

    [Fact]
    public async Task Delete_WithDefaultType_ShouldReturnValidationError()
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        var defaultType = (await ReadTypesAsync(client)).Single(type => type.IsSystem);

        // Act
        using var payload = await CallAsync(client, token, ToolNames.CardTypeDelete, new { boardId = 1, id = defaultType.Id });

        // Assert
        AssertError(payload, "validation_failed", 400);
        Assert.Contains(await ReadTypesAsync(client), item => item.Id == defaultType.Id && item.IsSystem);
    }

    [Theory]
    [InlineData(ToolNames.CardTypeSetDefault, 1, "default_set")]
    [InlineData(ToolNames.CardTypeDelete, int.MaxValue, "deleted")]
    public async Task Mutation_AlreadySatisfied_ShouldRemainIdempotent(string toolName, int id, string outcome)
    {
        // Arrange
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);

        // Act
        using var payload = await CallAsync(client, token, toolName, new { boardId = 1, id });

        // Assert
        var output = AssertSuccess(payload);
        Assert.Equal(id, output.GetProperty("id").GetInt32());
        Assert.Equal(outcome, output.GetProperty("outcome").GetString());
    }

    private static JsonObject DefinitionArguments(string toolName)
    {
        var arguments = new JsonObject { ["boardId"] = 1 };
        if (toolName != ToolNames.CardTypeCreate)
        {
            arguments["id"] = 1;
        }
        if (toolName == ToolNames.CardTypeCreate || toolName == ToolNames.CardTypeUpdate)
        {
            arguments["name"] = "MCP type";
            arguments["emoji"] = "🐈";
            arguments["style"] = StyleArgument("auto", "{}");
        }
        return arguments;
    }

    private static JsonObject StyleArgument(string name, string propertiesJson)
    {
        var style = new JsonObject { ["styleName"] = name };
        foreach (var property in JsonNode.Parse(propertiesJson)!.AsObject())
        {
            style[property.Key] = property.Value?.DeepClone();
        }
        return style;
    }

    private static void AssertDefinition(JsonElement snapshot, CardTypeDto stored)
    {
        Assert.Equal(stored.Name, snapshot.GetProperty("name").GetString());
        Assert.Equal(stored.Emoji, snapshot.GetProperty("emoji").GetString());
        Assert.Equal(stored.CreatedAtUtc, snapshot.GetProperty("createdAtUtc").GetDateTime());
        Assert.Equal(stored.UpdatedAtUtc, snapshot.GetProperty("updatedAtUtc").GetDateTime());
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(StyleArgument(stored.StyleName, stored.StylePropertiesJson)), snapshot.GetProperty("style")));
    }

    private static async Task<CardTypeDto> CreateTypeAsync(HttpClient client, string styleName = "auto", string propertiesJson = "{}")
    {
        using var response = await client.PostAsJsonAsync("/api/boards/1/card-types", new CreateCardTypeRequest("MCP type", "🐈", styleName, propertiesJson));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<CardTypeDto>>())!.Data!;
    }

    private static async Task<IReadOnlyList<CardTypeDto>> ReadTypesAsync(HttpClient client, int boardId = 1) =>
        (await client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<CardTypeDto>>>($"/api/boards/{boardId}/card-types"))!.Data!;

    private static async Task<CardDto> CreateCardAsync(HttpClient client, int? typeId)
    {
        using var response = await client.PostAsJsonAsync("/api/boards/1/cards", new CreateCardRequest(null, "Test card", "", [], typeId));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>())!.Data!;
    }

    private static JsonElement AssertSuccess(JsonDocument payload)
    {
        Assert.False(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        return McpJsonRpcClient.GetStructuredContent(payload);
    }

    private static JsonElement AssertError(JsonDocument payload, string code, int statusCode)
    {
        Assert.True(payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var error = McpJsonRpcClient.GetStructuredContent(payload);
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.Equal(statusCode, error.GetProperty("statusCode").GetInt32());
        return error;
    }

    private static async Task<JsonDocument> CallAsync(HttpClient client, string token, string toolName, object arguments)
    {
        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/call", new { name = toolName, arguments }, "card-type-mutation", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await McpJsonRpcClient.ParseJsonAsync(response);
    }
}
