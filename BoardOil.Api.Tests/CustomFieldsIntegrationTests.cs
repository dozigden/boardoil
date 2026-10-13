using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.CustomField;
using BoardOil.Contracts.CardType;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class CustomFieldsIntegrationTests : BoardApiIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public CustomFieldsIntegrationTests(DefaultApiFactoryFixture fixture) => UseSharedFactory(fixture);
    private const string Field = """{"definition":{"code":"Points","name":"Points","defaultValue":3,"dataType":"integer"},"appliesTo":{"mode":"all"}}""";

    [Fact]
    public async Task Catalogue_ShouldCreateReadEditReorderAndDeleteWithResolvedTypeProjection()
    {
        var created = await Client.PostAsync("/api/boards/1/custom-fields", Json(Field));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var second = await Client.PostAsync("/api/boards/1/custom-fields", Json("""
            {"definition":{"code":"Due","name":"Due date","nullable":true,"dataType":"date"},"appliesTo":{"mode":"all"}}
            """));
        second.EnsureSuccessStatusCode();
        var reordered = await Client.PutAsJsonAsync("/api/boards/1/custom-field-order", new CustomFieldOrderRequest(["Due", "Points"]));
        reordered.EnsureSuccessStatusCode();
        var catalogue = (await reordered.Content.ReadFromJsonAsync<ApiEnvelope<CustomFieldDto[]>>(JsonOptions))!.Data!;
        Assert.Equal(new[] { "Due", "Points" }, catalogue.Select(field => field.Definition.Code));
        var read = await Client.GetFromJsonAsync<ApiEnvelope<CustomFieldDto[]>>("/api/boards/1/custom-fields", JsonOptions);
        Assert.Equal(catalogue, read!.Data);
        var types = await Client.GetFromJsonAsync<ApiEnvelope<CardTypeDto[]>>("/api/boards/1/card-types", JsonOptions);
        Assert.All(types!.Data!, type => Assert.Equal(new[] { "Due", "Points" }, type.CustomFields.Select(field => field.Code)));

        var updated = await Client.PutAsync("/api/boards/1/custom-fields/Points", Json(Field.Replace("\"defaultValue\":3", "\"defaultValue\":4")));
        updated.EnsureSuccessStatusCode();
        var deleted = await Client.DeleteAsync("/api/boards/1/custom-fields/Due");
        deleted.EnsureSuccessStatusCode();
        Assert.Single((await deleted.Content.ReadFromJsonAsync<ApiEnvelope<CustomFieldDto[]>>(JsonOptions))!.Data!);
    }

    [Fact]
    public async Task DefinitionWrite_ShouldUpdateExistingCardsWhileTypeRenamePreservesDefinitions()
    {
        var column = await SeedBoardColumnAsync("Todo");
        var cardId = await SeedBoardCardAsync(column, "Existing", "");
        var typeId = await UseDbContextAsync(db => db.Cards.Where(card => card.BoardId == 1 && card.BoardCardId == cardId).Select(card => card.CardTypeId).SingleAsync());
        (await Client.PostAsync("/api/boards/1/custom-fields", Json(Field))).EnsureSuccessStatusCode();

        var response = await Client.PutAsJsonAsync($"/api/boards/1/card-types/{typeId}", new { name = "Renamed" });

        response.EnsureSuccessStatusCode();
        var type = (await response.Content.ReadFromJsonAsync<ApiEnvelope<CardTypeDto>>(JsonOptions))!.Data!;
        Assert.Equal(3, Assert.IsType<IntegerCustomFieldDefinition>(Assert.Single(type.CustomFields)).DefaultValue);
        await UseDbContextAsync(async db => Assert.Equal("{\"Points\":3}",
            (await db.Cards.SingleAsync(item => item.BoardId == 1 && item.BoardCardId == cardId)).CustomFieldValuesJson));
    }

    [Fact]
    public async Task Preview_ShouldBeReadOnlyAndDistinguishReplacementFromRemoval()
    {
        await SeedBoardColumnAsync("Todo");
        (await Client.PostAsync("/api/boards/1/custom-fields", Json(Field))).EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync("/api/boards/1/cards", new CreateCardRequest(null, "Card", "", []))).EnsureSuccessStatusCode();
        var proposed = Field.Replace("\"defaultValue\":3", "\"defaultValue\":0,\"max\":1");

        var response = await Client.PostAsync("/api/boards/1/custom-fields/Points/preview", Json("{\"field\":" + proposed + "}"));
        var removal = await Client.PostAsync("/api/boards/1/custom-fields/Points/preview", Json("{\"field\":null}"));

        Assert.Equal(new CustomFieldPreviewDto(1, 0, 1), (await response.Content.ReadFromJsonAsync<ApiEnvelope<CustomFieldPreviewDto>>(JsonOptions))!.Data);
        Assert.Equal(new CustomFieldPreviewDto(1, 1, 0), (await removal.Content.ReadFromJsonAsync<ApiEnvelope<CustomFieldPreviewDto>>(JsonOptions))!.Data);
        await UseDbContextAsync(async db =>
        {
            Assert.Equal("{\"Points\":3}", (await db.Cards.SingleAsync()).CustomFieldValuesJson);
            Assert.Empty(await db.CardComments.ToListAsync());
        });
    }

    [Theory]
    [InlineData("null")]
    [InlineData("""{"code":"F","name":null,"dataType":"text","nullable":true}""")]
    [InlineData("""{"code":"F","dataType":"text","nullable":true}""")]
    [InlineData("""{"code":"F","name":"Field","name":"Other","dataType":"text","nullable":true}""")]
    [InlineData("""{"code":"F","name":"Field","dataType":"date","maxLength":5,"nullable":true}""")]
    [InlineData("""{"code":"F","name":"Field","dataType":"boolean","nullable":true}""")]
    public async Task DefinitionWrites_ShouldRejectMalformedShapes(string definition)
    {
        var response = await Client.PostAsync("/api/boards/1/custom-fields", Json("{\"definition\":" + definition + ",\"appliesTo\":{\"mode\":\"all\"}}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await UseDbContextAsync(async db => Assert.Empty(await db.CustomFields.ToListAsync()));
    }

    [Theory]
    [InlineData("integer", "defaultValue", "\"abc\"", "Whole number required")]
    [InlineData("integer", "defaultValue", "1.5", "Whole number required")]
    [InlineData("integer", "min", "2147483648", "2147483647 maximum")]
    [InlineData("text", "maxLength", "\"abc\"", "Whole number required")]
    [InlineData("date", "defaultValue", "\"2026-02-30\"", "Date in YYYY-MM-DD format required")]
    public async Task DefinitionWrites_WithInvalidScalars_ShouldReturnPropertyValidation(string type, string property, string value, string message)
    {
        var body = $$$"""
            {"definition":{"dataType":"{{{type}}}","code":"F","name":"Field","nullable":true,"{{{property}}}":{{{value}}}},"appliesTo":{"mode":"all"}}
            """;

        var response = await Client.PostAsync("/api/boards/1/custom-fields", Json(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(message, payload.RootElement.GetProperty("validationErrors").GetProperty($"definition.{property}")[0].GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateAndPreview_WithInvalidScalars_ShouldKeepStructuredErrors(bool preview)
    {
        (await Client.PostAsync("/api/boards/1/custom-fields", Json(Field))).EnsureSuccessStatusCode();
        var body = Field.Replace("\"defaultValue\":3", "\"defaultValue\":\"bad\"");
        var response = preview
            ? await Client.PostAsync("/api/boards/1/custom-fields/Points/preview", Json("{\"field\":" + body + "}"))
            : await Client.PutAsync("/api/boards/1/custom-fields/Points", Json(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Whole number required", payload.RootElement.GetProperty("validationErrors").GetProperty("definition.defaultValue")[0].GetString());
    }

    [Fact]
    public async Task Create_WithMalformedJson_ShouldRemainAParsingFailure()
    {
        var response = await Client.PostAsync("/api/boards/1/custom-fields", Json("{\"definition\":"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");
}
