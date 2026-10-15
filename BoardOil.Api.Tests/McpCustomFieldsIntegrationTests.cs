using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Mcp.Contracts;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class McpCustomFieldsIntegrationTests : McpIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public McpCustomFieldsIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    [Fact]
    public async Task ArchivedReads_ShouldExposeCapturedFieldsAndDefinitionsThroughRestAndMcp()
    {
        var (client, token, typeId, columnId) = await ArrangeAsync();
        var created = await CallAsync(client, token, ToolNames.CardCreate,
            new { boardId = 1, columnId, cardTypeId = typeId, title = "Archive fields", description = "",
                customFieldValues = new { Points = 0, Ref = (string?)null } });
        var id = created.GetProperty("id").GetInt32();
        using var archive = await client.PostAsync($"/api/boards/1/cards/{id}/archive", null);
        archive.EnsureSuccessStatusCode();

        var archived = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id, archived = true });
        using var rest = await client.GetAsync($"/api/boards/1/cards/archived/{id}");
        rest.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await rest.Content.ReadAsStringAsync());
        var detail = document.RootElement.GetProperty("data");

        Assert.Equal("""{"Ref":null,"Points":0,"Due":"2024-02-29"}""", archived.GetProperty("customFieldValues").GetRawText());
        Assert.Equal(archived.GetProperty("customFieldValues").GetRawText(), detail.GetProperty("card").GetProperty("customFieldValues").GetRawText());
        var definitions = archived.GetProperty("customFieldDefinitions");
        Assert.Equal(new[] { "text", "integer", "date" }, definitions.EnumerateArray().Select(field => field.GetProperty("dataType").GetString()));
        Assert.Equal("Reference", definitions[0].GetProperty("name").GetString());
        Assert.Equal(definitions.GetRawText(), detail.GetProperty("customFieldDefinitions").GetRawText());
    }

    [Fact]
    public async Task Create_WithPartialFields_ShouldExposeDefaultsAndValuesOnCardAndBoardReads()
    {
        var (client, token, typeId, columnId) = await ArrangeAsync();
        var arguments = new { boardId = 1, columnId, cardTypeId = typeId, title = "Custom values", description = "",
            customFieldValues = new { Points = 7, Due = (string?)null } };

        var created = await CallAsync(client, token, ToolNames.CardCreate, arguments);

        var id = created.GetProperty("id").GetInt32();
        var card = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id });
        Assert.Equal("{\"Ref\":\"Default\",\"Points\":7,\"Due\":null}", card.GetProperty("customFieldValues").GetRawText());
        var board = await CallAsync(client, token, ToolNames.BoardGet, new { id = 1 });
        var listed = board.GetProperty("columns").EnumerateArray().SelectMany(column => column.GetProperty("cards").EnumerateArray())
            .Single(item => item.GetProperty("id").GetInt32() == id);
        Assert.Equal(card.GetProperty("customFieldValues").GetRawText(), listed.GetProperty("customFieldValues").GetRawText());
    }

    [Fact]
    public async Task Options_ShouldExposeOrderedDiscriminatedDefinitions()
    {
        var (client, token, typeId, _) = await ArrangeAsync();

        var options = await CallAsync(client, token, ToolNames.CardOptionsGet, new { id = 1 });

        var fields = options.GetProperty("cardTypes").EnumerateArray().Single(type => type.GetProperty("id").GetInt32() == typeId)
            .GetProperty("customFields");
        Assert.Equal(new[] { "text", "integer", "date" }, fields.EnumerateArray().Select(field => field.GetProperty("dataType").GetString()));
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
        Assert.Equal("{\"Ref\":\"Default\",\"Points\":0,\"Due\":\"2024-02-29\"}", card.GetProperty("customFieldValues").GetRawText());
    }

    [Fact]
    public async Task Update_WithTypeChange_ShouldApplyOriginalFieldsBeforeConversion()
    {
        var (client, token, typeId, columnId) = await ArrangeAsync();
        using var targetResponse = await client.PostAsync("/api/boards/1/card-types", Json("""
            {"name":"Destination"}
            """));
        targetResponse.EnsureSuccessStatusCode();
        var targetId = (await targetResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        var catalogue = (await client.GetFromJsonAsync<JsonElement>("/api/boards/1/custom-fields")).GetProperty("data");
        var fieldId = catalogue.EnumerateArray().Single(field => field.GetProperty("definition").GetProperty("code").GetString() == "Points").GetProperty("id").GetInt32();
        (await client.PutAsJsonAsync($"/api/boards/1/custom-fields/{fieldId}", new {
            definition = new { dataType = "integer", code = "Points", name = "Points", defaultValue = 0 },
            appliesTo = new { mode = "selected", cardTypeIds = new[] { typeId, targetId } }
        })).EnsureSuccessStatusCode();
        var created = await CallAsync(client, token, ToolNames.CardCreate,
            new { boardId = 1, columnId, cardTypeId = typeId, title = "Convert", description = "" });
        var id = created.GetProperty("id").GetInt32();
        var arguments = UpdateArguments(id, targetId);
        arguments["customFieldValues"] = new { Ref = "Edited loss", Points = 7, Due = (string?)null };

        await CallAsync(client, token, ToolNames.CardUpdate, arguments);

        var card = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id });
        Assert.Equal(7, card.GetProperty("customFieldValues").GetProperty("Points").GetInt32());
        Assert.Equal(string.Empty, card.GetProperty("description").GetString());
        var comment = Assert.Single(card.GetProperty("comments").EnumerateArray());
        Assert.Contains("Reference: Edited loss", comment.GetProperty("text").GetString());
        Assert.True(comment.GetProperty("isSystem").GetBoolean());
        Assert.Equal(JsonValueKind.Null, comment.GetProperty("authorUserId").ValueKind);
    }

    [Theory]
    [InlineData("null", "customFieldValues")]
    [InlineData("[]", "customFieldValues")]
    [InlineData("{}", "customFieldValues.Points")]
    [InlineData("""{"Ref":null,"Points":"7","Due":null}""", "customFieldValues.Points")]
    [InlineData("""{"Ref":null,"Points":1.5,"Due":null}""", "customFieldValues.Points")]
    [InlineData("""{"Ref":null,"Points":2147483648,"Due":null}""", "customFieldValues.Points")]
    [InlineData("""{"Ref":null,"Points":0,"Due":"2024-02-30"}""", "customFieldValues.Due")]
    [InlineData("""{"Ref":null,"Points":0,"Points":1,"Due":null}""", "customFieldValues.Points")]
    [InlineData("""{"Ref":null,"Points":0,"points":1,"Due":null}""", "customFieldValues.points")]
    [InlineData("""{"Ref":null,"Points":0,"Due":null,"Unknown":1}""", "customFieldValues.Unknown")]
    public async Task Update_WithInvalidFields_ShouldReturnValidationAndLeaveCardUnchanged(string fieldsJson, string errorPath)
    {
        var (client, token, typeId, columnId) = await ArrangeAsync();
        var created = await CallAsync(client, token, ToolNames.CardCreate,
            new { boardId = 1, columnId, cardTypeId = typeId, title = "Unchanged", description = "" });
        var id = created.GetProperty("id").GetInt32();
        var arguments = UpdateArguments(id, typeId);
        using var fields = JsonDocument.Parse(fieldsJson);
        arguments["customFieldValues"] = fields.RootElement;

        var error = await CallAsync(client, token, ToolNames.CardUpdate, arguments, expectError: true);

        Assert.Equal("validation_failed", error.GetProperty("code").GetString());
        Assert.True(error.GetProperty("validationErrors").TryGetProperty(errorPath, out _));
        var card = await CallAsync(client, token, ToolNames.CardGet, new { boardId = 1, id });
        Assert.Equal("Unchanged", card.GetProperty("title").GetString());
        Assert.Equal(0, card.GetProperty("customFieldValues").GetProperty("Points").GetInt32());
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
            Assert.DoesNotContain(schema.GetProperty("required").EnumerateArray(), property => property.GetString() == "customFieldValues");
            Assert.Equal("object", schema.GetProperty("properties").GetProperty("customFieldValues").GetProperty("type").GetString());
            Assert.Equal(3, schema.GetProperty("properties").GetProperty("customFieldValues").GetProperty("additionalProperties").GetProperty("anyOf").GetArrayLength());
        }
        var definitions = McpJsonRpcClient.GetToolByName(payload, ToolNames.CardOptionsGet).GetProperty("outputSchema")
            .GetProperty("properties").GetProperty("cardTypes").GetProperty("items").GetProperty("properties").GetProperty("customFields");
        Assert.Equal(3, definitions.GetProperty("items").GetProperty("oneOf").GetArrayLength());
        var live = McpJsonRpcClient.GetToolByName(payload, ToolNames.CardGet).GetProperty("outputSchema").GetProperty("oneOf")[0];
        Assert.Contains(live.GetProperty("required").EnumerateArray(), property => property.GetString() == "customFieldValues");
    }

    private async Task<(HttpClient Client, string Token, int TypeId, int ColumnId)> ArrangeAsync()
    {
        var client = CreateClient();
        await RegisterInitialAdminAsync(client);
        var token = await CreateMachinePatAsync(client);
        using var columnResponse = await client.PostAsJsonAsync("/api/boards/1/columns", new { title = "CustomFieldValues" });
        columnResponse.EnsureSuccessStatusCode();
        var columnId = (await columnResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        // Definitions enter through REST; MCP has no card-type write tool. Exercise metadata order on that transport.
        using var typeResponse = await client.PostAsJsonAsync("/api/boards/1/card-types", new { name = "MCP fields" });
        typeResponse.EnsureSuccessStatusCode();
        var typeId = (await typeResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        using var definitions = JsonDocument.Parse("""
            [
              {"code":"Ref","name":"Reference","nullable":true,"defaultValue":"Default","dataType":"text"},
              {"code":"Points","name":"Points","defaultValue":0,"dataType":"integer"},
              {"code":"Due","name":"Due date","nullable":true,"defaultValue":"2024-02-29","dataType":"date"}
            ]
            """);
        foreach (var definition in definitions.RootElement.EnumerateArray())
        {
            (await client.PostAsJsonAsync("/api/boards/1/custom-fields", new { definition, appliesTo = new { mode = "selected", cardTypeIds = new[] { typeId } } })).EnsureSuccessStatusCode();
        }
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
