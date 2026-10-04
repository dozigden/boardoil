using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.CardField;
using BoardOil.Contracts.CardType;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class CardTypeFieldsIntegrationTests : BoardApiIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public CardTypeFieldsIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    [Fact]
    public async Task Create_ShouldAcceptTypeAfterOtherPropertiesAndReturnOrderedDefinitions()
    {
        const string body = """
            {"name":"Configured","fields":[
                {"fieldCode":"Due","displayName":"Due date","defaultValue":"2024-02-29","type":"date"},
                {"displayName":"Estimate","type":"integer","fieldCode":"Points","defaultValue":0,"min":0},
                {"fieldCode":"Ref","displayName":"Reference","nullable":true,"type":"text","maxLength":250}
            ]}
            """;

        var response = await Client.PostAsync("/api/boards/1/card-types", Json(body));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<ApiEnvelope<CardTypeDto>>(JsonOptions))!.Data!;
        Assert.Equal(new[] { "Due", "Points", "Ref" }, created.Fields.Select(field => field.FieldCode));
        Assert.Equal(new DateOnly(2024, 2, 29), Assert.IsType<DateFieldDefinition>(created.Fields[0]).DefaultValue);
        Assert.Equal(0, Assert.IsType<IntegerFieldDefinition>(created.Fields[1]).DefaultValue);
        Assert.Equal(250, Assert.IsType<TextFieldDefinition>(created.Fields[2]).MaxLength);
        var listed = await Client.GetFromJsonAsync<ApiEnvelope<CardTypeDto[]>>("/api/boards/1/card-types", JsonOptions);
        Assert.Equal(created.Fields, Assert.Single(listed!.Data!, type => type.Id == created.Id).Fields);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("date", document.RootElement.GetProperty("data").GetProperty("fields")[0].GetProperty("type").GetString());
    }

    [Fact]
    public async Task Update_ShouldApplyFieldsToExistingCardsAndPreserveDefinitionsWhenOmitted()
    {
        var column = await SeedBoardColumnAsync("Todo");
        var cardId = await SeedBoardCardAsync(column, "Existing", "");
        var typeId = await UseDbContextAsync(db => db.Cards.Where(card => card.BoardId == 1 && card.BoardCardId == cardId).Select(card => card.CardTypeId).SingleAsync());
        var update = await Client.PutAsync($"/api/boards/1/card-types/{typeId}", Json("""
            {"name":"Configured","fields":[{"fieldCode":"Points","displayName":"Points","defaultValue":3,"type":"integer"}]}
            """));
        update.EnsureSuccessStatusCode();

        var response = await Client.PutAsJsonAsync($"/api/boards/1/card-types/{typeId}", new { name = "Legacy rename" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var type = (await response.Content.ReadFromJsonAsync<ApiEnvelope<CardTypeDto>>(JsonOptions))!.Data!;
        Assert.Equal(3, Assert.IsType<IntegerFieldDefinition>(Assert.Single(type.Fields)).DefaultValue);
        await UseDbContextAsync(async db =>
        {
            var card = await db.Cards.SingleAsync(item => item.BoardId == 1 && item.BoardCardId == cardId);
            Assert.Equal("{\"Points\":3}", card.FieldValuesJson);
        });
    }

    [Fact]
    public async Task Update_WithTighterLimits_ShouldReturnAffectedCardValidationAndPreserveDefinitions()
    {
        await SeedBoardColumnAsync("Todo");
        var createType = await Client.PostAsJsonAsync("/api/boards/1/card-types", new CreateCardTypeRequest("Configured", Fields:
            [new IntegerFieldDefinition { FieldCode = "Points", DisplayName = "Points", DefaultValue = 10 }]));
        createType.EnsureSuccessStatusCode();
        var type = (await createType.Content.ReadFromJsonAsync<ApiEnvelope<CardTypeDto>>(JsonOptions))!.Data!;
        var createCard = await Client.PostAsJsonAsync("/api/boards/1/cards", new CreateCardRequest(null, "Card", "", [], type.Id));
        createCard.EnsureSuccessStatusCode();
        var card = (await createCard.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions))!.Data!;

        var response = await Client.PutAsJsonAsync($"/api/boards/1/card-types/{type.Id}", new UpdateCardTypeRequest("Do not rename", Fields:
            [new IntegerFieldDefinition { FieldCode = "Points", DisplayName = "Points", DefaultValue = 0, Max = 5 }]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(payload.RootElement.GetProperty("validationErrors").TryGetProperty($"cards[{card.Id}].fields.Points", out _));
        await UseDbContextAsync(async db =>
        {
            Assert.Equal(type.Name, (await db.CardTypes.SingleAsync(item => item.Id == type.Id)).Name);
        });
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("""[{"fieldCode":"F","displayName":"Field","type":"integer","defaultValue":"1"}]""")]
    [InlineData("""[{"fieldCode":"F","displayName":null,"type":"text","nullable":true}]""")]
    [InlineData("""[{"fieldCode":"F","type":"text","nullable":true}]""")]
    [InlineData("""[{"fieldCode":"F","displayName":"Field","displayName":"Other","type":"text","nullable":true}]""")]
    [InlineData("""[{"fieldCode":"F","displayName":"Field","type":"date","maxLength":5,"nullable":true}]""")]
    [InlineData("""[{"fieldCode":"F","displayName":"Field","type":"boolean","nullable":true}]""")]
    public async Task DefinitionWrites_ShouldRejectMalformedShapes(string fields)
    {
        var typeId = await SeedBoardCardTypeAsync("Unchanged");
        var body = "{\"name\":\"Do not save\",\"fields\":" + fields + "}";

        var createResponse = await Client.PostAsync("/api/boards/1/card-types", Json(body));
        var updateResponse = await Client.PutAsync($"/api/boards/1/card-types/{typeId}", Json(body));

        Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
        await UseDbContextAsync(async db =>
        {
            Assert.False(await db.CardTypes.AnyAsync(type => type.Name == "Do not save"));
            Assert.Equal("Unchanged", (await db.CardTypes.SingleAsync(type => type.Id == typeId)).Name);
        });
    }

    [Theory]
    [InlineData("integer", "defaultValue", "\"abc\"", "Whole number required")]
    [InlineData("integer", "defaultValue", "1.5", "Whole number required")]
    [InlineData("integer", "min", "2147483648", "2147483647 maximum")]
    [InlineData("text", "maxLength", "\"abc\"", "Whole number required")]
    [InlineData("date", "defaultValue", "\"2026-02-30\"", "Date in YYYY-MM-DD format required")]
    public async Task DefinitionWrites_WithInvalidScalars_ShouldReturnPropertyValidation(
        string type, string property, string value, string message)
    {
        var body = $$"""
            {"name":"Invalid scalar","fields":[{"type":"{{type}}","fieldCode":"F","displayName":"Field","nullable":true,"{{property}}":{{value}}}]}
            """;

        var response = await Client.PostAsync("/api/boards/1/card-types", Json(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = payload.RootElement.GetProperty("validationErrors");
        Assert.Equal(message, errors.GetProperty($"fields[0].{property}")[0].GetString());
        await UseDbContextAsync(async db => Assert.False(await db.CardTypes.AnyAsync(item => item.Name == "Invalid scalar")));
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");
}
