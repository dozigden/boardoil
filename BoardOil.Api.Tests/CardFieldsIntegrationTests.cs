using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.CardField;
using BoardOil.Services.CardField;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class CardFieldsIntegrationTests : BoardApiIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public CardFieldsIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    [Fact]
    public async Task Create_WithTitleAndType_ShouldReturnDefaultsAndExposeThemOnBoardReads()
    {
        var typeId = await SeedTypeAsync();

        var response = await Client.PostAsJsonAsync("/api/boards/1/cards", new { title = "Defaults", cardTypeId = typeId });
        var boardResponse = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/boards/1", JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var card = (await response.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions))!.Data!;
        Assert.Equal(0, card.Fields["Points"].GetInt32());
        Assert.Equal(JsonValueKind.Null, card.Fields["Note"].ValueKind);
        Assert.Equal("2024-02-29", card.Fields["Due"].GetString());
        Assert.Equal(3, card.Fields.Count);
        var listed = Assert.Single(boardResponse!.Data!.Columns.SelectMany(column => column.Cards), item => item.Id == card.Id);
        Assert.Equal(CardFieldJsonCodec.SerialiseValues(card.Fields), CardFieldJsonCodec.SerialiseValues(listed.Fields));
    }

    [Fact]
    public async Task Update_ShouldRoundTripNativeValuesAndPreserveThemWhenOmitted()
    {
        var card = await CreateCardAsync();
        var fields = CardFieldJsonCodec.DeserialiseValues("{\"Points\":7,\"Note\":\"  Reference  \",\"Due\":null}");
        var update = await Client.PutAsJsonAsync($"/api/boards/1/cards/{card.Id}",
            new UpdateCardRequest(card.Title, "", [], card.CardTypeId, Fields: fields));
        update.EnsureSuccessStatusCode();

        var response = await Client.PutAsJsonAsync($"/api/boards/1/cards/{card.Id}",
            new { title = "Legacy client edit", description = "", tagNames = Array.Empty<string>(), cardTypeId = card.CardTypeId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var returned = json.RootElement.GetProperty("data").GetProperty("fields");
        Assert.Equal(7, returned.GetProperty("Points").GetInt32());
        Assert.Equal("Reference", returned.GetProperty("Note").GetString());
        Assert.Equal(JsonValueKind.Null, returned.GetProperty("Due").ValueKind);
        await UseDbContextAsync(async db =>
        {
            var stored = await db.Cards.SingleAsync(item => item.BoardId == 1 && item.BoardCardId == card.Id);
            Assert.Equal("{\"Points\":7,\"Note\":\"Reference\",\"Due\":null}", stored.FieldValuesJson);
        });
    }

    [Fact]
    public async Task Update_WithInvalidFieldValue_ShouldReturnFieldSpecificValidation()
    {
        var card = await CreateCardAsync();
        var fields = CardFieldJsonCodec.DeserialiseValues("{\"Points\":-1,\"Note\":null,\"Due\":null}");

        var response = await Client.PutAsJsonAsync($"/api/boards/1/cards/{card.Id}",
            new UpdateCardRequest("Do not save", "", [], card.CardTypeId, Fields: fields));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("validationErrors").TryGetProperty("fields.Points", out _));
    }

    [Theory]
    [InlineData("2147483648")]
    [InlineData("1.5")]
    public async Task Update_WithInvalidNumericShape_ShouldReturnFieldSpecificValidation(string value)
    {
        var card = await CreateCardAsync();
        var body = $$$"""{"title":"Do not save","description":"","tagNames":[],"cardTypeId":{{{card.CardTypeId}}},"fields":{"Points":{{{value}}},"Note":null,"Due":null}}""";

        var response = await Client.PutAsync($"/api/boards/1/cards/{card.Id}", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Whole number required", json.RootElement.GetProperty("validationErrors").GetProperty("fields.Points")[0].GetString());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"Points\":1,\"Points\":2,\"Note\":null,\"Due\":null}")]
    [InlineData("{\"Points\":1,\"points\":2,\"Note\":null,\"Due\":null}")]
    [InlineData("{\"Points\":true,\"Note\":null,\"Due\":null}")]
    public async Task Update_WithMalformedFieldDictionary_ShouldReturnBadRequest(string fieldsJson)
    {
        var card = await CreateCardAsync();
        var body = $$"""{"title":"Do not save","description":"","tagNames":[],"cardTypeId":{{card.CardTypeId}},"fields":{{fieldsJson}}} """;

        var response = await Client.PutAsync($"/api/boards/1/cards/{card.Id}", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await UseDbContextAsync(async db =>
        {
            var stored = await db.Cards.SingleAsync(item => item.BoardId == 1 && item.BoardCardId == card.Id);
            Assert.Equal(card.Title, stored.Title);
        });
    }

    private async Task<CardDto> CreateCardAsync()
    {
        var typeId = await SeedTypeAsync();
        var response = await Client.PostAsJsonAsync("/api/boards/1/cards", new { title = "Fields", cardTypeId = typeId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions))!.Data!;
    }

    private async Task<int> SeedTypeAsync()
    {
        await SeedBoardColumnAsync("Fields");
        var typeId = await SeedBoardCardTypeAsync("Fields");
        await ArrangeAsync(async db =>
        {
            var type = await db.CardTypes.SingleAsync(item => item.Id == typeId);
            type.FieldDefinitionsJson = CardFieldJsonCodec.SerialiseDefinitions(
            [
                new IntegerFieldDefinition { FieldCode = "Points", DisplayName = "Points", DefaultValue = 0, Min = 0 },
                new TextFieldDefinition { FieldCode = "Note", DisplayName = "Note", Nullable = true },
                new DateFieldDefinition { FieldCode = "Due", DisplayName = "Due date", Nullable = true, DefaultValue = new DateOnly(2024, 2, 29) }
            ]);
        });
        return typeId;
    }
}
