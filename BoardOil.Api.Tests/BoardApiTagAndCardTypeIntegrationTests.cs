using System.Net;
using System.Net.Http.Json;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.CardType;
using BoardOil.Contracts.Column;
using BoardOil.Contracts.Tag;
using BoardOil.Contracts.Users;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class BoardApiTagAndCardTypeIntegrationTests
    : BoardApiIntegrationTestBase
{
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
        var request = new UpdateTagStyleRequest("Bug", "gradient", """{"leftColor":"#223344","rightColor":"#446688","textColorMode":"auto"}""", "⚠️");
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
        Assert.Equal("gradient", patchedTagEnvelope.Data!.StyleName);
        Assert.Equal("⚠️", patchedTagEnvelope.Data.Emoji);
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
    public async Task CardTypeEndpoints_ShouldCreateListUpdateAndDelete_WithCardReassignment()
    {
        // Arrange
        var createColumnResponse = await Client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Todo"));
        createColumnResponse.EnsureSuccessStatusCode();
        var columnEnvelope = await createColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(columnEnvelope);
        Assert.NotNull(columnEnvelope!.Data);

        var createCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(columnEnvelope.Data!.Id, "Task A", "Desc", null));
        createCardResponse.EnsureSuccessStatusCode();
        var cardEnvelope = await createCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);
        Assert.NotNull(cardEnvelope);
        Assert.NotNull(cardEnvelope!.Data);

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
        Assert.False(createdTypeEnvelope.Data!.IsSystem);
        Assert.Equal("Bug", createdTypeEnvelope.Data.Name);
        Assert.Equal("🐞", createdTypeEnvelope.Data.Emoji);

        // Act: list
        var listEnvelope = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<CardTypeDto>>>("/api/boards/1/card-types", JsonOptions);

        // Assert: list
        Assert.NotNull(listEnvelope);
        Assert.NotNull(listEnvelope!.Data);
        Assert.Contains(listEnvelope.Data!, x => x.IsSystem && x.Name == "Story");
        Assert.Contains(listEnvelope.Data!, x => x.Name == "Bug");
        var systemType = Assert.Single(listEnvelope.Data!, x => x.IsSystem);
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
        Assert.False(updatedTypeEnvelope.Data.IsSystem);

        await AssignCardTypeToCardAsync(cardEnvelope.Data!.Id, bugType.Id);

        // Act: delete non-system
        var deleteTypeResponse = await Client.DeleteAsync($"/api/boards/1/card-types/{bugType.Id}");
        deleteTypeResponse.EnsureSuccessStatusCode();

        // Assert: card reassigned
        var reassignedCardTypeId = await GetCardTypeIdForCardAsync(cardEnvelope.Data.Id);
        Assert.Equal(systemType.Id, reassignedCardTypeId);

        var listAfterDelete = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<CardTypeDto>>>("/api/boards/1/card-types", JsonOptions);
        Assert.NotNull(listAfterDelete);
        Assert.NotNull(listAfterDelete!.Data);
        Assert.DoesNotContain(listAfterDelete.Data!, x => x.Id == bugType.Id);
    }

    [Fact]
    public async Task CardTypeEndpoints_SetDefault_ShouldUseNewDefaultForCreatedCards()
    {
        // Arrange
        var createColumnResponse = await Client.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Todo"));
        createColumnResponse.EnsureSuccessStatusCode();
        var columnEnvelope = await createColumnResponse.Content.ReadFromJsonAsync<ApiEnvelope<ColumnDto>>(JsonOptions);
        Assert.NotNull(columnEnvelope);
        Assert.NotNull(columnEnvelope!.Data);

        var createTypeResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/card-types",
            new CreateCardTypeRequest("Bug", "🐞"));
        createTypeResponse.EnsureSuccessStatusCode();
        var createdTypeEnvelope = await createTypeResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardTypeDto>>(JsonOptions);
        Assert.NotNull(createdTypeEnvelope);
        Assert.NotNull(createdTypeEnvelope!.Data);

        // Act: switch default card type
        var setDefaultResponse = await Client.PatchAsync($"/api/boards/1/card-types/{createdTypeEnvelope.Data!.Id}/default", null);
        setDefaultResponse.EnsureSuccessStatusCode();

        // Assert: card type flags updated
        var listEnvelope = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<CardTypeDto>>>("/api/boards/1/card-types", JsonOptions);
        Assert.NotNull(listEnvelope);
        Assert.NotNull(listEnvelope!.Data);
        var bugType = Assert.Single(listEnvelope.Data!, x => x.Name == "Bug");
        var storyType = Assert.Single(listEnvelope.Data!, x => x.Name == "Story");
        Assert.True(bugType.IsSystem);
        Assert.False(storyType.IsSystem);

        // Assert: create-card default follows switched card type
        var createCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(columnEnvelope.Data.Id, "Task with default", "Desc", null));
        createCardResponse.EnsureSuccessStatusCode();
        var createdCardEnvelope = await createCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);
        Assert.NotNull(createdCardEnvelope);
        Assert.NotNull(createdCardEnvelope!.Data);
        Assert.Equal(bugType.Id, createdCardEnvelope.Data!.CardTypeId);
        Assert.Equal("Bug", createdCardEnvelope.Data.CardTypeName);
        Assert.Equal("🐞", createdCardEnvelope.Data.CardTypeEmoji);
    }

    [Fact]
    public async Task CardTypeEndpoints_WhenDeletingSystemType_ShouldReturnBadRequest()
    {
        // Arrange
        var listEnvelope = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<CardTypeDto>>>("/api/boards/1/card-types", JsonOptions);
        Assert.NotNull(listEnvelope);
        Assert.NotNull(listEnvelope!.Data);
        var systemType = Assert.Single(listEnvelope.Data!, x => x.IsSystem);

        // Act
        var response = await Client.DeleteAsync($"/api/boards/1/card-types/{systemType.Id}");
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions);

        // Assert
        Assert.Equal(400, (int)response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Equal(400, payload.StatusCode);
        Assert.Equal("System card type cannot be deleted.", payload.Message);
    }

    [Fact]
    public async Task CardEndpoints_WhenAssignedUserNotInBoard_ShouldReturnValidationError()
    {
        var createUserResponse = await Client.PostAsJsonAsync(
            "/api/system/users",
            new CreateUserRequest("non-member", "non-member@localhost", "Password1234!", "Standard"));
        createUserResponse.EnsureSuccessStatusCode();
        var createdUserEnvelope = await createUserResponse.Content.ReadFromJsonAsync<ApiEnvelope<ManagedUserDto>>(JsonOptions);
        Assert.NotNull(createdUserEnvelope);
        Assert.NotNull(createdUserEnvelope!.Data);

        var boardEnvelope = await Client.GetFromJsonAsync<ApiEnvelope<BoardDto>>("/api/boards/1", JsonOptions);
        Assert.NotNull(boardEnvelope);
        Assert.NotNull(boardEnvelope!.Data);
        var todoColumnId = boardEnvelope.Data!.Columns[0].Id;

        var createCardResponse = await Client.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(todoColumnId, "Assignable", "Desc", []));
        createCardResponse.EnsureSuccessStatusCode();
        var createdCardEnvelope = await createCardResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);
        Assert.NotNull(createdCardEnvelope);
        Assert.NotNull(createdCardEnvelope!.Data);

        var updateResponse = await Client.PutAsJsonAsync(
            $"/api/boards/1/cards/{createdCardEnvelope.Data!.Id}",
            new UpdateCardRequest(
                createdCardEnvelope.Data.Title,
                createdCardEnvelope.Data.Description,
                createdCardEnvelope.Data.TagNames,
                createdCardEnvelope.Data.CardTypeId,
                createdCardEnvelope.Data.BoardColumnId,
                createdUserEnvelope.Data!.Id));
        var validationEnvelope = await updateResponse.Content.ReadFromJsonAsync<ApiEnvelope<CardDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
        Assert.NotNull(validationEnvelope);
        Assert.False(validationEnvelope!.Success);
        Assert.NotNull(validationEnvelope.ValidationErrors);
        Assert.True(validationEnvelope.ValidationErrors!.ContainsKey("assignedUserId"));
    }

}
