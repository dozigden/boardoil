using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Card;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Tests.Infrastructure;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardTextSearchServiceTests : TestBaseDb
{
    [Theory]
    [InlineData("title", "CAFÉ")]
    [InlineData("description", "CAFÉ")]
    [InlineData("externalUrl", "CAFÉ")]
    [InlineData("number", "234")]
    public async Task Search_ShouldMatchEachTextFieldAndScopeToBoard(string field, string query)
    {
        // Arrange
        CreateBoard("Other board").AddColumn("Todo").AddCard("Other café", "café").Build();
        var board = CreateBoard().AddColumn("Todo").AddCard("Matching", "").AddCard("Unrelated", "").Build();
        var card = board.GetCard("Todo", "Matching");
        switch (field)
        {
            case "title": card.Title = "A café story"; break;
            case "description": card.Description = "A café story"; break;
            case "externalUrl": card.ExternalUrl = "https://example.com/café"; break;
            case "number": card.BoardCardId = 12345; break;
        }
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardService>();

        // Act
        var result = await service.SearchCardsByTextAsync(board.BoardId, new(query), ActorUserId);

        // Assert
        Assert.True(result.Success);
        var summary = Assert.Single(result.Data!.Cards);
        Assert.Equal(card.BoardCardId, summary.Id);
        Assert.Equal(card.Title, summary.Title);
        Assert.Equal(card.BoardColumnId, summary.ColumnId);
        Assert.Equal(card.CardTypeId, summary.CardTypeId);
        Assert.Equal(card.ExternalUrl, summary.ExternalUrl);
        Assert.Empty(summary.TagNames);
        Assert.Null(summary.SlickName);
        Assert.Equal(1, result.Data.TotalCount);
        Assert.Equal(0, result.Data.Offset);
        Assert.Equal(20, result.Data.Limit);
    }

    [Theory]
    [InlineData("%_")]
    [InlineData(".*[abc](x)+?\\")]
    [InlineData(" café ")]
    public async Task Search_ShouldTreatQueryAsLiteralIncludingWhitespace(string query)
    {
        // Arrange
        var board = CreateBoard().AddColumn("Todo")
            .AddCard("Match", "prefix" + query + "suffix")
            .AddCard("No match café", "unrelated")
            .Build();
        var service = ResolveService<ICardService>();

        // Act
        var result = await service.SearchCardsByTextAsync(board.BoardId, new(query), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Match", Assert.Single(result.Data!.Cards).Title);
    }

    [Fact]
    public async Task Search_ShouldReturnTagAndSlickNamesWithoutParsingStyles()
    {
        // Arrange
        var board = CreateBoard().AddColumn("Todo").AddCard("Find me", "").Build();
        var card = board.GetCard("Todo", "Find me");
        AddContext(card, board.BoardId);
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardService>();

        // Act
        var result = await service.SearchCardsByTextAsync(board.BoardId, new("Find"), ActorUserId);

        // Assert
        Assert.True(result.Success);
        var summary = Assert.Single(result.Data!.Cards);
        Assert.Equal(["Tag-only"], summary.TagNames);
        Assert.Equal("Slick-only", summary.SlickName);
    }

    [Theory]
    [InlineData("Tag-only")]
    [InlineData("Slick-only")]
    [InlineData("Comment-only")]
    [InlineData("Archived-only")]
    public async Task Search_ShouldNotMatchRelatedFieldsOrArchivedCards(string query)
    {
        // Arrange
        var board = CreateBoard().AddColumn("Todo").AddCard("Live", "").Build();
        var card = board.GetCard("Todo", "Live");
        AddContext(card, board.BoardId);
        card.Comments.Add(new EntityCardComment { Text = "Comment-only", PostedAtUtc = DateTime.UtcNow });
        DbContextForArrange.ArchivedCards.Add(new EntityArchivedCard
        {
            BoardId = board.BoardId, OriginalCardId = 99, ArchivedAtUtc = DateTime.UtcNow,
            SearchTitle = "Archived-only", SearchTextNormalised = "ARCHIVED-ONLY",
            SearchTagsJson = "[]", SnapshotJson = "{}"
        });
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardService>();

        // Act
        var result = await service.SearchCardsByTextAsync(board.BoardId, new(query), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Data!.Cards);
        Assert.Equal(0, result.Data.TotalCount);
    }

    [Theory]
    [InlineData(0, 2, "Third,Second")]
    [InlineData(1, 2, "Second,First")]
    [InlineData(3, 2, "")]
    [InlineData(int.MaxValue, 100, "")]
    public async Task Search_ShouldPageInColumnThenCardOrder(int offset, int limit, string expectedTitles)
    {
        // Arrange
        var board = CreateBoard().AddColumn("First column")
            .AddCard("First", "needle").AddCard("Second", "needle")
            .AddColumn("Second column").AddCard("Third", "needle").Build();
        // Reverse both column order and card order so neither primary-key nor title order suffices.
        board.GetColumn("First column").SortKey = new string('2', 20);
        board.GetColumn("Second column").SortKey = new string('1', 20);
        board.GetCard("First column", "First").SortKey = new string('2', 20);
        board.GetCard("First column", "Second").SortKey = new string('1', 20);
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardService>();

        // Act
        var result = await service.SearchCardsByTextAsync(board.BoardId, new("needle", offset, limit), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedTitles, string.Join(",", result.Data!.Cards.Select(card => card.Title)));
        Assert.Equal(3, result.Data.TotalCount);
        Assert.Equal(offset, result.Data.Offset);
        Assert.Equal(limit, result.Data.Limit);
    }

    [Theory]
    [InlineData(null, 0, 20, "query")]
    [InlineData("", 0, 20, "query")]
    [InlineData(" \t\n", 0, 20, "query")]
    [InlineData("x", -1, 20, "offset")]
    [InlineData("x", 0, 0, "limit")]
    [InlineData("x", 0, 101, "limit")]
    public async Task Search_ShouldValidateQueryAndPagination(string? query, int offset, int limit, string field)
    {
        // Arrange
        var board = CreateBoard().AddColumn("Todo").Build();
        var service = ResolveService<ICardService>();

        // Act
        var result = await service.SearchCardsByTextAsync(board.BoardId, new(query!, offset, limit), ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains(result.ValidationErrors!, error => error.Key == field);
    }

    private static void AddContext(EntityBoardCard card, int boardId)
    {
        // Legacy invalid styles must not prevent text searches from returning the canonical names.
        card.Slick = new EntitySlick
        {
            BoardId = boardId, Name = "Slick-only", NormalisedName = "SLICK-ONLY",
            StyleName = "invalid", StylePropertiesJson = "broken"
        };
        card.CardTags.Add(new EntityCardTag
        {
            Tag = new EntityTag
            {
                BoardId = boardId, Name = "Tag-only", NormalisedName = "TAG-ONLY",
                StyleName = "invalid", StylePropertiesJson = "broken"
            }
        });
    }
}
