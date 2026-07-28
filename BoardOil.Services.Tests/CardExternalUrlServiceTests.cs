using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Card;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardExternalUrlServiceTests : TestBaseDb
{
    [Fact]
    public async Task CreateAndUpdateCardAsync_ShouldNormalisePersistAndClearExternalUrl()
    {
        // Arrange
        var board = CreateBoard("External links")
            .AddColumn("Todo")
            .Build();
        var columnId = board.GetColumn("Todo").Id;
        var cardTypeId = await DbContextForArrange.CardTypes
            .Where(x => x.BoardId == board.BoardId && x.IsSystem)
            .Select(x => x.Id)
            .SingleAsync();
        var service = ResolveService<ICardService>();

        // Act
        var createResult = await service.CreateCardAsync(
            board.BoardId,
            new CreateCardRequest(
                columnId,
                "Linked card",
                "Description",
                [],
                ExternalUrl: "  https://github.com/example/repository  "),
            ActorUserId);
        var updateResult = await service.UpdateCardAsync(
            board.BoardId,
            createResult.Data!.Id,
            new UpdateCardRequest(
                "Linked card",
                "Description",
                [],
                cardTypeId,
                ExternalUrl: "   "),
            ActorUserId);

        // Assert
        Assert.True(createResult.Success);
        Assert.Equal("https://github.com/example/repository", createResult.Data.ExternalUrl);
        Assert.True(updateResult.Success);
        Assert.Null(updateResult.Data!.ExternalUrl);
        var storedExternalUrl = await DbContextForAssert.Cards
            .Where(x => x.Id == createResult.Data.Id)
            .Select(x => x.ExternalUrl)
            .SingleAsync();
        Assert.Null(storedExternalUrl);
    }

    [Theory]
    [InlineData("github.com/example/repository")]
    [InlineData("ftp://example.com/file")]
    [InlineData("javascript:alert(1)")]
    public async Task CreateCardAsync_WhenExternalUrlIsNotHttpOrHttps_ShouldReturnValidationError(string externalUrl)
    {
        // Arrange
        var board = CreateBoard("External links")
            .AddColumn("Todo")
            .Build();
        var columnId = board.GetColumn("Todo").Id;
        var service = ResolveService<ICardService>();

        // Act
        var result = await service.CreateCardAsync(
            board.BoardId,
            new CreateCardRequest(columnId, "Linked card", "Description", [], ExternalUrl: externalUrl),
            ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains(result.ValidationErrors!, x => x.Key == "externalUrl");
        Assert.Empty(DbContextForAssert.Cards);
    }

    [Fact]
    public async Task SearchCardsAsync_WhenExternalUrlIsExact_ShouldReturnAllMatchesOnRequestedBoard()
    {
        // Arrange
        const string ExternalUrl = "https://github.com/example/repository";
        var board = CreateBoard("External links")
            .AddColumn("Todo")
            .AddCard("First", "Description")
            .AddCard("Second", "Description")
            .Build();
        var otherBoard = CreateBoard("Other board")
            .AddColumn("Todo")
            .AddCard("Other", "Description")
            .Build();
        foreach (var card in DbContextForArrange.Cards)
        {
            card.ExternalUrl = ExternalUrl;
        }
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardService>();

        // Act
        var result = await service.SearchCardsAsync(
            board.BoardId,
            new SearchCardsRequest([
                new CardSearchFilterRequest(
                    CardSearchFields.ExternalUrl,
                    CardSearchOperators.Exact,
                    ExternalUrl)
            ]),
            ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(["First", "Second"], result.Data!.Select(x => x.Title).ToArray());
        Assert.DoesNotContain(result.Data, x => x.Id == otherBoard.GetCard("Todo", "Other").Id);
    }

    [Fact]
    public async Task SearchCardsAsync_WhenExternalUrlContainsValue_ShouldMatchCaseInsensitiveLiteralSubstring()
    {
        // Arrange
        var board = CreateBoard("External links")
            .AddColumn("Todo")
            .AddCard("Matching", "Description")
            .AddCard("Other", "Description")
            .Build();
        board.GetCard("Todo", "Matching").ExternalUrl = "https://GitHub.com/Example/café";
        board.GetCard("Todo", "Other").ExternalUrl = "https://example.com/elsewhere";
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardService>();

        // Act
        var result = await service.SearchCardsAsync(
            board.BoardId,
            new SearchCardsRequest([
                new CardSearchFilterRequest(
                    CardSearchFields.ExternalUrl,
                    CardSearchOperators.Contains,
                    "github.COM/example/CAFÉ")
            ]),
            ActorUserId);

        // Assert
        Assert.True(result.Success);
        var matchingCard = Assert.Single(result.Data!);
        Assert.Equal("Matching", matchingCard.Title);
    }

    [Fact]
    public async Task SearchCardsAsync_WhenMultipleFiltersAreProvided_ShouldRequireAllToMatch()
    {
        // Arrange
        const string ExternalUrl = "https://github.com/example/repository";
        var board = CreateBoard("External links")
            .AddColumn("Todo")
            .AddCard("Matching", "Description")
            .AddCard("Contains only", "Description")
            .Build();
        board.GetCard("Todo", "Matching").ExternalUrl = ExternalUrl;
        board.GetCard("Todo", "Contains only").ExternalUrl = $"{ExternalUrl}/issues";
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardService>();

        // Act
        var result = await service.SearchCardsAsync(
            board.BoardId,
            new SearchCardsRequest([
                new CardSearchFilterRequest(
                    CardSearchFields.ExternalUrl,
                    CardSearchOperators.Contains,
                    "github.com/example"),
                new CardSearchFilterRequest(
                    CardSearchFields.ExternalUrl,
                    CardSearchOperators.Exact,
                    ExternalUrl)
            ]),
            ActorUserId);

        // Assert
        Assert.True(result.Success);
        var matchingCard = Assert.Single(result.Data!);
        Assert.Equal("Matching", matchingCard.Title);
    }

    [Theory]
    [InlineData("", "exact", "value")]
    [InlineData("github.com/example", "", "operator")]
    [InlineData("github.com/example", "prefix", "operator")]
    public async Task SearchCardsAsync_WhenFilterIsInvalid_ShouldReturnValidationError(
        string value,
        string searchOperator,
        string expectedField)
    {
        // Arrange
        var board = CreateBoard("External links")
            .AddColumn("Todo")
            .Build();
        var service = ResolveService<ICardService>();

        // Act
        var result = await service.SearchCardsAsync(
            board.BoardId,
            new SearchCardsRequest([
                new CardSearchFilterRequest(
                    CardSearchFields.ExternalUrl,
                    searchOperator,
                    value)
            ]),
            ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains(result.ValidationErrors!, x => x.Key == $"filters[0].{expectedField}");
    }

    [Fact]
    public async Task SearchCardsAsync_WhenFiltersAreEmpty_ShouldReturnValidationError()
    {
        // Arrange
        var board = CreateBoard("External links")
            .AddColumn("Todo")
            .Build();
        var service = ResolveService<ICardService>();

        // Act
        var result = await service.SearchCardsAsync(
            board.BoardId,
            new SearchCardsRequest([]),
            ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains(result.ValidationErrors!, x => x.Key == "filters");
    }

    [Fact]
    public async Task SearchCardsAsync_WhenTenFiltersAreProvided_ShouldSearchSuccessfully()
    {
        // Arrange
        var board = CreateBoard("External links")
            .AddColumn("Todo")
            .AddCard("Matching", "Description")
            .Build();
        board.GetCard("Todo", "Matching").ExternalUrl = "https://example.com/repository";
        await DbContextForArrange.SaveChangesAsync();
        var filters = Enumerable.Range(0, 10)
            .Select(_ => new CardSearchFilterRequest(
                CardSearchFields.ExternalUrl,
                CardSearchOperators.Contains,
                "example"))
            .ToList();
        var service = ResolveService<ICardService>();

        // Act
        var result = await service.SearchCardsAsync(
            board.BoardId,
            new SearchCardsRequest(filters),
            ActorUserId);

        // Assert
        Assert.True(result.Success);
        var matchingCard = Assert.Single(result.Data!);
        Assert.Equal("Matching", matchingCard.Title);
    }

    [Fact]
    public async Task SearchCardsAsync_WhenMoreThanTenFiltersAreProvided_ShouldReturnValidationError()
    {
        // Arrange
        var board = CreateBoard("External links")
            .AddColumn("Todo")
            .Build();
        var filters = Enumerable.Range(0, 11)
            .Select(_ => new CardSearchFilterRequest(
                CardSearchFields.ExternalUrl,
                CardSearchOperators.Contains,
                "example"))
            .ToList();
        var service = ResolveService<ICardService>();

        // Act
        var result = await service.SearchCardsAsync(
            board.BoardId,
            new SearchCardsRequest(filters),
            ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains(result.ValidationErrors!, x => x.Key == "filters");
    }

    [Fact]
    public async Task SearchCardsAsync_WhenFieldIsUnsupported_ShouldReturnValidationError()
    {
        // Arrange
        var board = CreateBoard("External links")
            .AddColumn("Todo")
            .Build();
        var service = ResolveService<ICardService>();

        // Act
        var result = await service.SearchCardsAsync(
            board.BoardId,
            new SearchCardsRequest([
                new CardSearchFilterRequest("title", CardSearchOperators.Exact, "Card")
            ]),
            ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains(result.ValidationErrors!, x => x.Key == "filters[0].field");
    }
}
