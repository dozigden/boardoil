using System.Net;
using System.Net.Http.Json;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Slick;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class BoardApiSlickIntegrationTests : BoardApiIntegrationTestBase
{
    [Fact]
    public async Task SlickEndpoints_ShouldCreateListUpdateAndDeleteSlick()
    {
        // Create
        var createResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/slicks",
            new CreateSlickRequest("Release train", "presets", """{"presetIndex":2}"""));
        createResponse.EnsureSuccessStatusCode();
        var createdEnvelope = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<SlickDto>>(JsonOptions);

        Assert.NotNull(createdEnvelope);
        Assert.NotNull(createdEnvelope!.Data);
        Assert.Equal(201, createdEnvelope.StatusCode);
        Assert.Equal("Release train", createdEnvelope.Data!.Name);

        // List
        var listEnvelope = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<SlickDto>>>("/api/boards/1/slicks", JsonOptions);
        Assert.NotNull(listEnvelope);
        Assert.NotNull(listEnvelope!.Data);
        var slick = Assert.Single(listEnvelope.Data!);

        // Update
        var updateResponse = await Client.PutAsJsonAsync(
            $"/api/boards/1/slicks/{slick.Id}",
            new UpdateSlickRequest("Release train", "solid", """{"backgroundColor":"#336699","textColorMode":"auto","borderMode":"auto"}"""));
        updateResponse.EnsureSuccessStatusCode();
        var updatedEnvelope = await updateResponse.Content.ReadFromJsonAsync<ApiEnvelope<SlickDto>>(JsonOptions);

        Assert.NotNull(updatedEnvelope);
        Assert.NotNull(updatedEnvelope!.Data);
        Assert.Equal("solid", updatedEnvelope.Data!.StyleName);

        // Delete
        var deleteResponse = await Client.DeleteAsync($"/api/boards/1/slicks/{slick.Id}");
        deleteResponse.EnsureSuccessStatusCode();
        var listAfterDelete = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<SlickDto>>>("/api/boards/1/slicks", JsonOptions);
        Assert.NotNull(listAfterDelete);
        Assert.NotNull(listAfterDelete!.Data);
        Assert.Empty(listAfterDelete.Data!);
    }

    [Fact]
    public async Task SlickEndpoints_WhenStyleInvalid_ShouldReturnBadRequest()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/boards/1/slicks",
            new CreateSlickRequest("Release train", "gradient", """{"leftColor":"#111111","rightColor":"#222222"}"""));
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal(400, payload.StatusCode);
    }

    [Fact]
    public async Task CardEndpoints_ShouldPersistSlickIdAcrossCreateAndUpdate()
    {
        var slickCreateResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/slicks",
            new CreateSlickRequest("Release train", "presets", """{"presetIndex":2}"""));
        slickCreateResponse.EnsureSuccessStatusCode();
        var slickEnvelope = await slickCreateResponse.Content.ReadFromJsonAsync<ApiEnvelope<SlickDto>>(JsonOptions);
        Assert.NotNull(slickEnvelope);
        Assert.NotNull(slickEnvelope!.Data);
        var slickId = slickEnvelope.Data!.Id;

        var columnId = await SeedBoardColumnAsync("Todo");

        var createCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(columnId, "Card A", "Desc", [], null, null, slickEnvelope.Data.Name));
        createCardResponse.EnsureSuccessStatusCode();
        var createdCardEnvelope = await createCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);
        Assert.NotNull(createdCardEnvelope);
        Assert.NotNull(createdCardEnvelope!.Data);
        Assert.Equal(slickId, createdCardEnvelope.Data!.SlickId);

        var updatedCardResponse = await Client.PutAsJsonAsync(
            $"/api/boards/1/cards/{createdCardEnvelope.Data.Id}",
            new UpdateCardRequest("Card A", "Desc", [], createdCardEnvelope.Data.CardTypeId, null, null, null));
        updatedCardResponse.EnsureSuccessStatusCode();
        var updatedCardEnvelope = await updatedCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);
        Assert.NotNull(updatedCardEnvelope);
        Assert.NotNull(updatedCardEnvelope!.Data);
        Assert.Null(updatedCardEnvelope.Data!.SlickId);
    }
}
