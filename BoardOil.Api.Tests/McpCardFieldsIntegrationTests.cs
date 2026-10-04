using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Mcp.Contracts;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpCardFieldsIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public McpCardFieldsIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    [Fact]
    public async Task ArchivedReads_ShouldExposeCapturedFieldsAndDefinitionsThroughRestAndMcp()
    {
        var (client, token, typeId, columnId) = await ArrangeAsync();
        var created = await CallAsync(client, token, ToolNames.CardCreate,
            new { boardId = 1, columnId, cardTypeId = typeId, title = "Archive fields", description = "",
                fields = new { Points = 0, Ref = (string?)null } });
        var id = created.GetProperty("id").GetInt32();
        using var archive = await client.PostAsync($"/api/boards/1/cards/{id}/archive", null);
        archive.EnsureSuccessStatusCode();

        var archived = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id, archived = true });
        using var rest = await client.GetAsync($"/api/boards/1/cards/archived/{id}");
        rest.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await rest.Content.ReadAsStringAsync());
        var detail = document.RootElement.GetProperty("data");

        Assert.Equal("""{"Ref":null,"Points":0,"Due":"2024-02-29"}""", archived.GetProperty("fields").GetRawText());
        Assert.Equal(archived.GetProperty("fields").GetRawText(), detail.GetProperty("card").GetProperty("fields").GetRawText());
        var definitions = archived.GetProperty("fieldDefinitions");
        Assert.Equal(new[] { "text", "integer", "date" }, definitions.EnumerateArray().Select(field => field.GetProperty("type").GetString()));
        Assert.Equal("Reference", definitions[0].GetProperty("displayName").GetString());
        Assert.Equal(definitions.GetRawText(), detail.GetProperty("fieldDefinitions").GetRawText());
    }

    [Fact]
    public async Task Create_WithPartialFields_ShouldExposeDefaultsAndValuesOnCardAndBoardReads()
    {
        var (client, token, typeId, columnId) = await ArrangeAsync();
        var arguments = new { boardId = 1, columnId, cardTypeId = typeId, title = "Custom values", description = "",
            fields = new { Points = 7, Due = (string?)null } };

        var created = await CallAsync(client, token, ToolNames.CardCreate, arguments);

        var id = created.GetProperty("id").GetInt32();
        var card = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id });
        Assert.Equal("{\"Ref\":\"Default\",\"Points\":7,\"Due\":null}", card.GetProperty("fields").GetRawText());
        var board = await CallAsync(client, token, ToolNames.BoardGet, new { id = 1 });
        var listed = board.GetProperty("columns").EnumerateArray().SelectMany(column => column.GetProperty("cards").EnumerateArray())
            .Single(item => item.GetProperty("id").GetInt32() == id);
        Assert.Equal(card.GetProperty("fields").GetRawText(), listed.GetProperty("fields").GetRawText());
    }

    [Fact]
    public async Task Options_ShouldExposeOrderedDiscriminatedDefinitions()
    {
        var (client, token, typeId, _) = await ArrangeAsync();

        var options = await CallAsync(client, token, ToolNames.CardOptionsGet, new { id = 1 });

        var fields = options.GetProperty("cardTypes").EnumerateArray().Single(type => type.GetProperty("id").GetInt32() == typeId)
            .GetProperty("fields");
        Assert.Equal(new[] { "text", "integer", "date" }, fields.EnumerateArray().Select(field => field.GetProperty("type").GetString()));
        Assert.Equal("Default", fields[0].GetProperty("defaultValue").GetString());
        Assert.Equal(0, fields[1].GetProperty("defaultValue").GetInt32());
        Assert.Equal("2024-02-29", fields[2].GetProperty("defaultValue").GetString());
        Assert.False(fields[2].TryGetProperty("maxLength", out _));
    }

    [Fact]
    public async Task Update_WhenFieldsOmitted_ShouldPreserveValuesForExistingClients()
    {
        var (client, token, typeId, columnId) = await ArrangeAsync();
        var created = await CallAsync(client, token, ToolNames.CardCreate,
            new { boardId = 1, columnId, cardTypeId = typeId, title = "Defaults", description = "" });
        var id = created.GetProperty("id").GetInt32();

        await CallAsync(client, token, ToolNames.CardUpdate, UpdateArguments(id, typeId));

        var card = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id });
        Assert.Equal("{\"Ref\":\"Default\",\"Points\":0,\"Due\":\"2024-02-29\"}", card.GetProperty("fields").GetRawText());
    }

    [Fact]
    public async Task Update_WithTypeChange_ShouldApplyOriginalFieldsBeforeConversion()
    {
        var (client, token, typeId, columnId) = await ArrangeAsync();
        using var targetResponse = await client.PostAsync("/api/boards/1/card-types", Json("""
            {"name":"Destination","fields":[{"type":"text","fieldCode":"Estimate","displayName":"Points","nullable":true}]}
            """));
        targetResponse.EnsureSuccessStatusCode();
        var targetId = (await targetResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        var created = await CallAsync(client, token, ToolNames.CardCreate,
            new { boardId = 1, columnId, cardTypeId = typeId, title = "Convert", description = "" });
        var id = created.GetProperty("id").GetInt32();
        var arguments = UpdateArguments(id, targetId);
        arguments["fields"] = new { Ref = "Edited loss", Points = 7, Due = (string?)null };

        await CallAsync(client, token, ToolNames.CardUpdate, arguments);

        var card = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id });
        Assert.Equal("7", card.GetProperty("fields").GetProperty("Estimate").GetString());
        Assert.Contains("Reference: Edited loss", card.GetProperty("description").GetString());
    }

    [Theory]
    [InlineData("null", "fields")]
    [InlineData("[]", "fields")]
    [InlineData("{}", "fields.Points")]
    [InlineData("""{"Ref":null,"Points":"7","Due":null}""", "fields.Points")]
    [InlineData("""{"Ref":null,"Points":1.5,"Due":null}""", "fields.Points")]
    [InlineData("""{"Ref":null,"Points":2147483648,"Due":null}""", "fields.Points")]
    [InlineData("""{"Ref":null,"Points":0,"Due":"2024-02-30"}""", "fields.Due")]
    [InlineData("""{"Ref":null,"Points":0,"Points":1,"Due":null}""", "fields.Points")]
    [InlineData("""{"Ref":null,"Points":0,"points":1,"Due":null}""", "fields.points")]
    [InlineData("""{"Ref":null,"Points":0,"Due":null,"Unknown":1}""", "fields.Unknown")]
    public async Task Update_WithInvalidFields_ShouldReturnValidationAndLeaveCardUnchanged(string fieldsJson, string errorPath)
    {
        var (client, token, typeId, columnId) = await ArrangeAsync();
        var created = await CallAsync(client, token, ToolNames.CardCreate,
            new { boardId = 1, columnId, cardTypeId = typeId, title = "Unchanged", description = "" });
        var id = created.GetProperty("id").GetInt32();
        var arguments = UpdateArguments(id, typeId);
        using var fields = JsonDocument.Parse(fieldsJson);
        arguments["fields"] = fields.RootElement;

        var error = await CallAsync(client, token, ToolNames.CardUpdate, arguments, expectError: true);

        Assert.Equal("validation_failed", error.GetProperty("code").GetString());
        Assert.True(error.GetProperty("validationErrors").TryGetProperty(errorPath, out _));
        var card = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id });
        Assert.Equal("Unchanged", card.GetProperty("title").GetString());
        Assert.Equal(0, card.GetProperty("fields").GetProperty("Points").GetInt32());
    }

    [Fact]
    public async Task Discovery_ShouldDescribeOptionalValuesAndDefinitionVariants()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);

        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/list", new { }, "field-schemas", token);
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);

        foreach (var name in new[] { ToolNames.CardCreate, ToolNames.CardUpdate })
        {
            var schema = McpJsonRpcClient.GetToolByName(payload, name).GetProperty("inputSchema");
            Assert.DoesNotContain(schema.GetProperty("required").EnumerateArray(), property => property.GetString() == "fields");
            Assert.Equal("object", schema.GetProperty("properties").GetProperty("fields").GetProperty("type").GetString());
            Assert.Equal(3, schema.GetProperty("properties").GetProperty("fields").GetProperty("additionalProperties").GetProperty("anyOf").GetArrayLength());
        }
        var definitions = McpJsonRpcClient.GetToolByName(payload, ToolNames.CardOptionsGet).GetProperty("outputSchema")
            .GetProperty("properties").GetProperty("cardTypes").GetProperty("items").GetProperty("properties").GetProperty("fields");
        Assert.Equal(3, definitions.GetProperty("items").GetProperty("oneOf").GetArrayLength());
        var live = McpJsonRpcClient.GetToolByName(payload, ToolNames.CardGet).GetProperty("outputSchema").GetProperty("oneOf")[0];
        Assert.Contains(live.GetProperty("required").EnumerateArray(), property => property.GetString() == "fields");
    }

    private async Task<(HttpClient Client, string Token, int TypeId, int ColumnId)> ArrangeAsync()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        using var columnResponse = await client.PostAsJsonAsync("/api/boards/1/columns", new { title = "Fields" });
        columnResponse.EnsureSuccessStatusCode();
        var columnId = (await columnResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        // Definitions enter through REST; MCP has no card-type write tool. Exercise metadata order on that transport.
        using var typeResponse = await client.PostAsync("/api/boards/1/card-types", Json("""
            {"name":"MCP fields","fields":[
              {"fieldCode":"Ref","displayName":"Reference","nullable":true,"defaultValue":"Default","type":"text"},
              {"fieldCode":"Points","displayName":"Points","defaultValue":0,"type":"integer"},
              {"fieldCode":"Due","displayName":"Due date","nullable":true,"defaultValue":"2024-02-29","type":"date"}
            ]}
            """));
        typeResponse.EnsureSuccessStatusCode();
        var typeId = (await typeResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        return (client, token, typeId, columnId);
    }

    private static Dictionary<string, object?> UpdateArguments(int id, int cardTypeId) => new()
    {
        ["boardId"] = 1, ["id"] = id, ["cardTypeId"] = cardTypeId, ["title"] = "Updated",
        ["description"] = "", ["tagNames"] = Array.Empty<string>(), ["slickName"] = null, ["externalUrl"] = null
    };

    private static async Task<JsonElement> CallAsync(HttpClient client, string token, string name, object arguments, bool expectError = false)
    {
        using var response = await McpJsonRpcClient.SendRequestAsync(client, "tools/call", new { name, arguments }, "card-fields", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = await McpJsonRpcClient.ParseJsonAsync(response);
        Assert.Equal(expectError, payload.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        return McpJsonRpcClient.GetStructuredContent(payload).Clone();
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");
}
