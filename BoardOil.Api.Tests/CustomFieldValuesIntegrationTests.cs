using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.CustomField;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.CustomField;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class CustomFieldValuesIntegrationTests : BoardApiIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public CustomFieldValuesIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);

    [Fact]
    public async Task Create_WithTitleAndType_ShouldReturnDefaultsAndExposeThemOnBoardReads()
    {
        var typeId = await SeedTypeAsync();

        var response = await Client.PostAsJsonAsync("/api/boards/1/cards", new { title = "Defaults", cardTypeId = typeId });
        var boardResponse = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/boards/1", JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var card = (await response.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions))!.Data!;
        Assert.Equal(0, card.CustomFieldValues["Points"].GetInt32());
        Assert.Equal(JsonValueKind.Null, card.CustomFieldValues["Note"].ValueKind);
        Assert.Equal("2024-02-29", card.CustomFieldValues["Due"].GetString());
        Assert.Equal(3, card.CustomFieldValues.Count);
        var listed = Assert.Single(boardResponse!.Data!.Columns.SelectMany(column => column.Cards), item => item.Id == card.Id);
        Assert.Equal(CustomFieldJsonCodec.SerialiseValues(card.CustomFieldValues), CustomFieldJsonCodec.SerialiseValues(listed.CustomFieldValues));
    }

    [Fact]
    public async Task Update_ShouldRoundTripNativeValuesAndPreserveThemWhenOmitted()
    {
        var card = await CreateCardAsync();
        var fields = CustomFieldJsonCodec.DeserialiseValues("{\"Points\":7,\"Note\":\"  Reference  \",\"Due\":null}");
        var update = await Client.PutAsJsonAsync($"/api/boards/1/cards/{card.Id}",
            new UpdateCardRequest(card.Title, "", [], card.CardTypeId, CustomFieldValues: fields));
        update.EnsureSuccessStatusCode();

        var response = await Client.PutAsJsonAsync($"/api/boards/1/cards/{card.Id}",
            new { title = "Legacy client edit", description = "", tagNames = Array.Empty<string>(), cardTypeId = card.CardTypeId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var returned = json.RootElement.GetProperty("data").GetProperty("customFieldValues");
        Assert.Equal(7, returned.GetProperty("Points").GetInt32());
        Assert.Equal("Reference", returned.GetProperty("Note").GetString());
        Assert.Equal(JsonValueKind.Null, returned.GetProperty("Due").ValueKind);
        await UseDbContextAsync(async db =>
        {
            var stored = await db.Cards.SingleAsync(item => item.BoardId == 1 && item.BoardCardId == card.Id);
            Assert.Equal("{\"Points\":7,\"Note\":\"Reference\",\"Due\":null}", stored.CustomFieldValuesJson);
        });
    }

    [Fact]
    public async Task Update_WithInvalidFieldValue_ShouldReturnFieldSpecificValidation()
    {
        var card = await CreateCardAsync();
        var fields = CustomFieldJsonCodec.DeserialiseValues("{\"Points\":-1,\"Note\":null,\"Due\":null}");

        var response = await Client.PutAsJsonAsync($"/api/boards/1/cards/{card.Id}",
            new UpdateCardRequest("Do not save", "", [], card.CardTypeId, CustomFieldValues: fields));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("validationErrors").TryGetProperty("customFieldValues.Points", out _));
    }

    [Theory]
    [InlineData("2147483648")]
    [InlineData("1.5")]
    public async Task Update_WithInvalidNumericShape_ShouldReturnFieldSpecificValidation(string value)
    {
        var card = await CreateCardAsync();
        var body = $$$"""{"title":"Do not save","description":"","tagNames":[],"cardTypeId":{{{card.CardTypeId}}},"customFieldValues":{"Points":{{{value}}},"Note":null,"Due":null}}""";

        var response = await Client.PutAsync($"/api/boards/1/cards/{card.Id}", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Whole number required", json.RootElement.GetProperty("validationErrors").GetProperty("customFieldValues.Points")[0].GetString());
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
        var body = $$"""{"title":"Do not save","description":"","tagNames":[],"cardTypeId":{{card.CardTypeId}},"customFieldValues":{{fieldsJson}}} """;

        var response = await Client.PutAsync($"/api/boards/1/cards/{card.Id}", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await UseDbContextAsync(async db =>
        {
            var stored = await db.Cards.SingleAsync(item => item.BoardId == 1 && item.BoardCardId == card.Id);
            Assert.Equal(card.Title, stored.Title);
        });
    }

    [Fact]
    public async Task Duplicate_ShouldBindFieldValuesWithoutAddingThemToNormalCreation()
    {
        var source = await CreateCardAsync();
        var fields = CustomFieldJsonCodec.DeserialiseValues("""{"Points":7,"Note":"Copy only","Due":null}""");

        var response = await Client.PostAsJsonAsync($"/api/boards/1/cards/{source.Id}/duplicate",
            new DuplicateCardRequest(source.BoardColumnId, "Duplicate", "", [], source.CardTypeId, CustomFieldValues: fields));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var duplicate = (await response.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions))!.Data!;
        Assert.NotEqual(source.Id, duplicate.Id);
        Assert.Equal(7, duplicate.CustomFieldValues["Points"].GetInt32());
        Assert.Equal(JsonValueKind.Null, duplicate.CustomFieldValues["Due"].ValueKind);
        await UseDbContextAsync(async db =>
        {
            var stored = await db.Cards.SingleAsync(card => card.BoardId == 1 && card.BoardCardId == source.Id);
            Assert.Equal(0, CustomFieldJsonCodec.DeserialiseValues(stored.CustomFieldValuesJson)["Points"].GetInt32());
        });
        Assert.Null(typeof(CreateCardRequest).GetProperty("CustomFieldValues"));
    }

    [Fact]
    public async Task TypeChangePreview_ShouldReturnAlignedNativeValuesWithoutSaving()
    {
        var card = await CreateCardAsync();
        var targetId = await SeedBoardCardTypeAsync("Target");
        await ArrangeAsync(async db =>
        {
            var target = await db.CardTypes.Include(type => type.Board).ThenInclude(board => board.CustomFields).ThenInclude(field => field.CardTypeAssignments).SingleAsync(type => type.Id == targetId);
            var field = target.Board.CustomFields.Single(field => field.Code == "Points");
            field.ApplyCustomFieldDraft(field.ToCustomFieldDraft() with
            {
                AppliesTo = new SelectedCardTypes([card.CardTypeId, targetId])
            });
        });
        var body = $$$"""{"cardTypeId":{{{targetId}}},"customFieldValues":{"Points":7,"Note":"Draft","Due":null}}""";

        var response = await Client.PostAsync($"/api/boards/1/cards/{card.Id}/type-change/preview",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = json.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("requiresConfirmation").GetBoolean());
        Assert.True(data.GetProperty("hasLosses").GetBoolean());
        var rows = data.GetProperty("rows");
        Assert.Equal(7, rows[0].GetProperty("original").GetProperty("value").GetInt32());
        Assert.Equal(7, rows[0].GetProperty("destination").GetProperty("value").GetInt32());
        Assert.Equal("integer", rows[0].GetProperty("destination").GetProperty("dataType").GetString());
        Assert.Equal(JsonValueKind.Null, rows[1].GetProperty("destination").ValueKind);
        Assert.True(rows[1].GetProperty("losesValue").GetBoolean());
        await UseDbContextAsync(async db =>
        {
            var stored = await db.Cards.SingleAsync(item => item.BoardId == 1 && item.BoardCardId == card.Id);
            Assert.Equal(card.CardTypeId, stored.CardTypeId);
            Assert.Equal(0, CustomFieldJsonCodec.DeserialiseValues(stored.CustomFieldValuesJson)["Points"].GetInt32());
            Assert.Empty(await db.CardComments.Where(comment => comment.CardId == stored.Id).ToListAsync());
        });
    }

    [Fact]
    public async Task TypeChangePreview_ShouldReturnOriginalFieldValidation()
    {
        var card = await CreateCardAsync();
        var body = $$$"""{"cardTypeId":{{{card.CardTypeId}}},"customFieldValues":{"Points":"invalid","Note":null,"Due":null}}""";

        var response = await Client.PostAsync($"/api/boards/1/cards/{card.Id}/type-change/preview",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Whole number required", json.RootElement.GetProperty("validationErrors").GetProperty("customFieldValues.Points")[0].GetString());
    }

    private async Task<CardDto> CreateCardAsync()
    {
        var typeId = await SeedTypeAsync();
        var response = await Client.PostAsJsonAsync("/api/boards/1/cards", new { title = "CustomFieldValues", cardTypeId = typeId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions))!.Data!;
    }

    private async Task<int> SeedTypeAsync()
    {
        await SeedBoardColumnAsync("CustomFieldValues");
        var typeId = await SeedBoardCardTypeAsync("CustomFieldValues");
        await ArrangeAsync(async db =>
        {
            var type = await db.CardTypes.Include(type => type.Board).ThenInclude(board => board.CustomFields).ThenInclude(field => field.CardTypeAssignments).SingleAsync(item => item.Id == typeId);
            var definitions = new CustomFieldDefinition[]
            {
                new IntegerCustomFieldDefinition { Code = "Points", Name = "Points", DefaultValue = 0, Min = 0 },
                new TextCustomFieldDefinition { Code = "Note", Name = "Note", Nullable = true },
                new DateCustomFieldDefinition { Code = "Due", Name = "Due date", Nullable = true, DefaultValue = new DateOnly(2024, 2, 29) }
            };
            foreach (var definition in definitions)
            {
                var field = new EntityCustomField { Board = type.Board, BoardId = type.BoardId, SortOrder = type.Board.CustomFields.Count };
                field.ApplyCustomFieldDraft(new(definition, new SelectedCardTypes([typeId])));
                type.Board.CustomFields.Add(field);
            }
        });
        return typeId;
    }
}
