using System.Net;
using System.Net.Http.Json;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.CardType;
using BoardOil.Contracts.Column;
using BoardOil.Contracts.Tag;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class AuthAuthorisationBoardAccessIntegrationTests : AuthAuthorisationIntegrationTestBase
{
    [Fact]
    public async Task AnonymousUser_GetBoard_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/boards/1");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_WithoutBoardMembership_GetBoard_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        var standardClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.GetAsync("/api/boards/1");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_WithoutBoardMembership_ArchiveCardsBulk_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        var standardClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        var columnId = await SeedBoardColumnAsync("Todo");
        var cardId = await SeedBoardCardAsync(columnId, "Archive target", "Desc");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.PostAsJsonAsync(
            "/api/boards/1/cards/archive",
            new ArchiveCardsRequest([cardId]));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_CreateCard_ShouldReturnCreated()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        var standardClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var memberUserId = await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await AddBoardMemberAsAdminAsync(adminClient, 1, memberUserId, "Contributor");
        var columnId = await SeedBoardColumnAsync("Todo");
        _ = await SeedBoardTagAsync("member");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.PostAsJsonAsync(
            "/api/boards/1/cards",
            new CreateCardRequest(columnId, "Standard card", "Allowed", ["member"]));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_UpdateAndDeleteCardType_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        var standardClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var memberUserId = await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await AddBoardMemberAsAdminAsync(adminClient, 1, memberUserId, "Contributor");
        var cardTypeId = await SeedBoardCardTypeAsync("Feature");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var updateResponse = await standardClient.PutAsJsonAsync(
            $"/api/boards/1/card-types/{cardTypeId}",
            new UpdateCardTypeRequest("Platform"));
        var setDefaultResponse = await standardClient.PatchAsync($"/api/boards/1/card-types/{cardTypeId}/default", null);
        var deleteResponse = await standardClient.DeleteAsync($"/api/boards/1/card-types/{cardTypeId}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, setDefaultResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task StandardUser_UpdateTagStyle_ShouldReturnOk()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        var standardClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var memberUserId = await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await AddBoardMemberAsAdminAsync(adminClient, 1, memberUserId, "Contributor");
        _ = await SeedBoardTagAsync("member");
        await LoginAsAsync(standardClient, "member", "Password1234!");
        var tagsEnvelope = await standardClient.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<BoardTagDto>>>("/api/boards/1/tags");
        Assert.NotNull(tagsEnvelope);
        Assert.NotNull(tagsEnvelope!.Data);
        var memberTag = Assert.Single(tagsEnvelope.Data!, x => x.Name == "member");

        // Act
        var response = await standardClient.PutAsJsonAsync(
            $"/api/boards/1/tags/{memberTag.Id}",
            new UpdateTagStyleRequest(
                Name: "member",
                StyleName: "solid",
                StylePropertiesJson: """{"backgroundColor":"#113355","textColorMode":"auto"}"""));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_CreateColumn_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = Factory.CreateClient();
        var standardClient = Factory.CreateClient();
        await RegisterInitialAdminAsync(adminClient);
        var memberUserId = await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        await AddBoardMemberAsAdminAsync(adminClient, 1, memberUserId, "Contributor");
        await LoginAsAsync(standardClient, "member", "Password1234!");

        // Act
        var response = await standardClient.PostAsJsonAsync("/api/boards/1/columns", new CreateColumnRequest("Not allowed"));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed record BoardTagDto(int Id, string Name);
}
