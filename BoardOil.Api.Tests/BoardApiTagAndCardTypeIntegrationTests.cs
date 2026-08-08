using System.Net;
using System.Net.Http.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.CardType;
using BoardOil.Contracts.Tag;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class BoardApiTagAndCardTypeIntegrationTests
    : BoardApiIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public BoardApiTagAndCardTypeIntegrationTests(DefaultApiFactoryFixture fixture)
    {
        UseSharedFactory(fixture);
    }

    [Fact]
    public async Task TagEndpoints_ShouldCreateTag()
    {
        // Arrange
        var request = new CreateTagRequest("Bug", "🐞");

        // Act
        var createResponse = await Client.PostAsJsonAsync("/api/boards/1/tags", request);
        createResponse.EnsureSuccessStatusCode();
        var createdTagEnvelope = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<TagDto>>(JsonOptions);

        // Assert
        Assert.NotNull(createdTagEnvelope);
        Assert.NotNull(createdTagEnvelope!.Data);
        Assert.Equal("Bug", createdTagEnvelope.Data!.Name);
        Assert.Equal("🐞", createdTagEnvelope.Data.Emoji);
        Assert.Equal(201, createdTagEnvelope.StatusCode);
    }

    [Fact]
    public async Task TagEndpoints_ShouldUpdateTagStyles()
    {
        // Arrange
        await SeedTagAsync("Bug", "BUG", "solid", """{"backgroundColor":"#224466","textColorMode":"auto"}""");
        var request = new UpdateTagStyleRequest("Bug", "presets", """{"presetIndex":4,"textColorMode":"auto","borderMode":"auto"}""", "⚠️");
        var tagsEnvelope = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<TagDto>>>("/api/boards/1/tags", JsonOptions);
        Assert.NotNull(tagsEnvelope);
        Assert.NotNull(tagsEnvelope!.Data);
        var bugTag = Assert.Single(tagsEnvelope.Data!, x => x.Name == "Bug");

        // Act
        var putResponse = await Client.PutAsJsonAsync(
            $"/api/boards/1/tags/{bugTag.Id}",
            request);
        putResponse.EnsureSuccessStatusCode();

        // Assert
        var patchedTagEnvelope = await putResponse.Content.ReadFromJsonAsync<ApiEnvelope<TagDto>>(JsonOptions);
        Assert.NotNull(patchedTagEnvelope);
        Assert.NotNull(patchedTagEnvelope!.Data);
        Assert.Equal("presets", patchedTagEnvelope.Data!.StyleName);
        Assert.Equal("⚠️", patchedTagEnvelope.Data.Emoji);
    }

    [Fact]
    public async Task TagEndpoints_ShouldAcceptOpaqueStyleJsonObject()
    {
        await SeedTagAsync("Bug", "BUG", "solid", """{"backgroundColor":"#224466","textColorMode":"auto"}""");
        var request = new UpdateTagStyleRequest("Bug", "solid", """{"unexpected":"shape","nested":{"x":1}}""");
        var tagsEnvelope = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<TagDto>>>("/api/boards/1/tags", JsonOptions);
        Assert.NotNull(tagsEnvelope);
        Assert.NotNull(tagsEnvelope!.Data);
        var bugTag = Assert.Single(tagsEnvelope.Data!, x => x.Name == "Bug");

        var putResponse = await Client.PutAsJsonAsync($"/api/boards/1/tags/{bugTag.Id}", request);
        putResponse.EnsureSuccessStatusCode();
        var patchedTagEnvelope = await putResponse.Content.ReadFromJsonAsync<ApiEnvelope<TagDto>>(JsonOptions);

        Assert.NotNull(patchedTagEnvelope);
        Assert.NotNull(patchedTagEnvelope!.Data);
        Assert.Equal("""{"unexpected":"shape","nested":{"x":1}}""", patchedTagEnvelope.Data!.StylePropertiesJson);
    }

    [Fact]
    public async Task TagEndpoints_WhenStyleJsonIsNotObject_ShouldReturnBadRequest()
    {
        await SeedTagAsync("Bug", "BUG", "solid", """{"backgroundColor":"#224466","textColorMode":"auto"}""");
        var request = new UpdateTagStyleRequest("Bug", "solid", """["bad"]""");
        var tagsEnvelope = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<TagDto>>>("/api/boards/1/tags", JsonOptions);
        Assert.NotNull(tagsEnvelope);
        Assert.NotNull(tagsEnvelope!.Data);
        var bugTag = Assert.Single(tagsEnvelope.Data!, x => x.Name == "Bug");

        var response = await Client.PutAsJsonAsync($"/api/boards/1/tags/{bugTag.Id}", request);
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal(400, payload.StatusCode);
    }

    [Fact]
    public async Task TagEndpoints_WhenTagIdMissing_ShouldReturnNotFound()
    {
        // Arrange
        var request = new UpdateTagStyleRequest("Bug", "solid", """{"backgroundColor":"#223344","textColorMode":"auto"}""");

        // Act
        var response = await Client.PutAsJsonAsync("/api/boards/1/tags/999999", request);
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions);

        // Assert
        Assert.Equal(404, (int)response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal(404, payload.StatusCode);
        Assert.Equal("Tag not found.", payload.Message);
    }

    [Fact]
    public async Task DeleteTag_WhenMissing_ShouldReturnOkContract()
    {
        // Act
        var response = await Client.DeleteAsync("/api/boards/1/tags/999999");
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions);

        // Assert
        Assert.Equal(200, (int)response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.Success);
        Assert.Equal(200, payload.StatusCode);
        Assert.Null(payload.Message);
    }

    [Fact]
    public async Task CardTypeEndpoints_ShouldCreateListUpdateAndDeleteCustomType()
    {
        // Arrange
        // Act: create
        var createTypeResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/card-types",
            new CreateCardTypeRequest("Bug", "🐞"));
        createTypeResponse.EnsureSuccessStatusCode();
        var createdTypeEnvelope = await createTypeResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardTypeDto>>(JsonOptions);

        // Assert: create
        Assert.NotNull(createdTypeEnvelope);
        Assert.NotNull(createdTypeEnvelope!.Data);
        Assert.Equal(201, createdTypeEnvelope.StatusCode);
        Assert.Equal("Bug", createdTypeEnvelope.Data.Name);
        Assert.Equal("🐞", createdTypeEnvelope.Data.Emoji);

        // Act: list
        var listEnvelope = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<CardTypeDto>>>("/api/boards/1/card-types", JsonOptions);

        // Assert: list
        Assert.NotNull(listEnvelope);
        Assert.NotNull(listEnvelope!.Data);
        Assert.Contains(listEnvelope.Data!, x => x.Name == "Bug");
        var bugType = Assert.Single(listEnvelope.Data!, x => x.Name == "Bug");

        // Act: update
        var updateTypeResponse = await Client.PutAsJsonAsync(
            $"/api/boards/1/card-types/{bugType.Id}",
            new UpdateCardTypeRequest("Defect", "⚠️"));
        updateTypeResponse.EnsureSuccessStatusCode();
        var updatedTypeEnvelope = await updateTypeResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardTypeDto>>(JsonOptions);

        // Assert: update
        Assert.NotNull(updatedTypeEnvelope);
        Assert.NotNull(updatedTypeEnvelope!.Data);
        Assert.Equal("Defect", updatedTypeEnvelope.Data!.Name);
        Assert.Equal("⚠️", updatedTypeEnvelope.Data.Emoji);

        // Act: update style mode
        var updateStyleResponse = await Client.PutAsJsonAsync(
            $"/api/boards/1/card-types/{bugType.Id}",
            new UpdateCardTypeRequest("Defect", "⚠️", "presets", """{"presetIndex":1,"textColorMode":"auto","borderMode":"auto"}"""));
        updateStyleResponse.EnsureSuccessStatusCode();
        var styledTypeEnvelope = await updateStyleResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardTypeDto>>(JsonOptions);

        // Assert: update style mode
        Assert.NotNull(styledTypeEnvelope);
        Assert.NotNull(styledTypeEnvelope!.Data);
        Assert.Equal("presets", styledTypeEnvelope.Data!.StyleName);

        // Act: delete non-system
        var deleteTypeResponse = await Client.DeleteAsync($"/api/boards/1/card-types/{bugType.Id}");
        deleteTypeResponse.EnsureSuccessStatusCode();

        // Assert: delete reflected in list
        var listAfterDelete = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<CardTypeDto>>>("/api/boards/1/card-types", JsonOptions);
        Assert.NotNull(listAfterDelete);
        Assert.NotNull(listAfterDelete!.Data);
        Assert.DoesNotContain(listAfterDelete.Data!, x => x.Id == bugType.Id);
    }

    [Fact]
    public async Task CardTypeEndpoints_ShouldAcceptOpaqueStyleJsonObject()
    {
        var createTypeResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/card-types",
            new CreateCardTypeRequest("Bug", "🐞"));
        createTypeResponse.EnsureSuccessStatusCode();
        var createdTypeEnvelope = await createTypeResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardTypeDto>>(JsonOptions);
        Assert.NotNull(createdTypeEnvelope);
        Assert.NotNull(createdTypeEnvelope!.Data);

        var updateStyleResponse = await Client.PutAsJsonAsync(
            $"/api/boards/1/card-types/{createdTypeEnvelope.Data!.Id}",
            new UpdateCardTypeRequest("Bug", "🐞", "solid", """{"unexpected":"shape"}"""));
        updateStyleResponse.EnsureSuccessStatusCode();
        var updatedTypeEnvelope = await updateStyleResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardTypeDto>>(JsonOptions);

        Assert.NotNull(updatedTypeEnvelope);
        Assert.NotNull(updatedTypeEnvelope!.Data);
        Assert.Equal("""{"unexpected":"shape"}""", updatedTypeEnvelope.Data!.StylePropertiesJson);
    }

    [Fact]
    public async Task CardTypeEndpoints_WhenStyleJsonIsNotObject_ShouldReturnBadRequest()
    {
        var createTypeResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/card-types",
            new CreateCardTypeRequest("Bug", "🐞"));
        createTypeResponse.EnsureSuccessStatusCode();
        var createdTypeEnvelope = await createTypeResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardTypeDto>>(JsonOptions);
        Assert.NotNull(createdTypeEnvelope);
        Assert.NotNull(createdTypeEnvelope!.Data);

        var response = await Client.PutAsJsonAsync(
            $"/api/boards/1/card-types/{createdTypeEnvelope.Data!.Id}",
            new UpdateCardTypeRequest("Bug", "🐞", "solid", """["bad"]"""));
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal(400, payload.StatusCode);
    }

    [Fact]
    public async Task CardTypeEndpoints_SetDefault_ShouldReturnOkContract()
    {
        // Arrange
        var createTypeResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/card-types",
            new CreateCardTypeRequest("Bug", "🐞"));
        createTypeResponse.EnsureSuccessStatusCode();
        var createdTypeEnvelope = await createTypeResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardTypeDto>>(JsonOptions);
        Assert.NotNull(createdTypeEnvelope);
        Assert.NotNull(createdTypeEnvelope!.Data);

        // Act
        var setDefaultResponse = await Client.PatchAsync($"/api/boards/1/card-types/{createdTypeEnvelope.Data!.Id}/default", null);
        var payload = await setDefaultResponse.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions);

        // Assert
        Assert.Equal(200, (int)setDefaultResponse.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.Success);
        Assert.Equal(200, payload.StatusCode);
    }

}
