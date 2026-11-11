using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Contracts.Board;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class BoardReorderServiceTests : TestBaseDb
{
    [Fact]
    public async Task Reorder_ShouldApplyExactLayoutUsingBoardScopedNumbersAndPreserveContents()
    {
        // Arrange
        CreateBoard("Other").AddColumn("Other").AddCard("Other card").Build();
        var board = CreateBoard().AddColumn("A").AddCard("One", "Description")
            .AddCard("Two").AddColumn("B").AddCard("Three").AddCard("Four")
            .AddColumn("Untouched").AddCard("Five").AddColumn("Empty").Build();
        var a = board.GetColumn("A").Id;
        var b = board.GetColumn("B").Id;
        var untouched = board.GetColumn("Untouched").Id;
        var empty = board.GetColumn("Empty").Id;
        var cards = await DbContextForArrange.Cards.Where(x => x.BoardId == board.BoardId).OrderBy(x => x.BoardCardId).ToListAsync();
        var oldTimestamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var slick = DbContextForArrange.Slicks.Add(new EntitySlick
        {
            BoardId = board.BoardId, Name = "Shared", NormalisedName = "SHARED",
            StyleName = "presets", StylePropertiesJson = """{"presetIndex":3}"""
        }).Entity;
        foreach (var card in cards)
        {
            card.BoardCardId += 100;
            card.CardUpdatedUtc = oldTimestamp;
        }
        cards[0].Slick = slick;
        cards[2].Slick = slick;
        cards[0].ExternalUrl = "https://example.test/one";
        cards[0].AssignedUserId = ActorUserId;
        await DbContextForArrange.SaveChangesAsync();
        var before = await ContentsAsync(DbContextForArrange);
        var columnsBefore = await DbContextForArrange.Columns.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Title, x.SortKey, x.UpdatedAtUtc }).ToArrayAsync();
        var untouchedBefore = cards[4].SortKey;

        // Act
        var result = await ResolveService<IBoardService>().ReorderBoardAsync(board.BoardId,
            new([new(b, [103, 104, 102, 101]), new(empty, []), new(a, []), new(untouched, [105])]), ActorUserId);

        // Assert
        Assert.True(result.Success);
        var aIds = await OrderedIdsAsync(a);
        var bIds = await OrderedIdsAsync(b);
        Assert.Empty(aIds);
        Assert.Equal([103, 104, 102, 101], bIds);
        Assert.Equal(before, await ContentsAsync(DbContextForAssert));
        Assert.Equal(columnsBefore, await DbContextForAssert.Columns.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Title, x.SortKey, x.UpdatedAtUtc }).ToArrayAsync());
        var stored = await DbContextForAssert.Cards.Where(x => x.BoardId == board.BoardId).OrderBy(x => x.BoardCardId).ToArrayAsync();
        Assert.True(stored[0].CardUpdatedUtc > oldTimestamp);
        Assert.True(stored[1].CardUpdatedUtc > oldTimestamp);
        Assert.Equal(oldTimestamp, stored[2].CardUpdatedUtc);
        Assert.Equal(oldTimestamp, stored[3].CardUpdatedUtc);
        Assert.Equal(oldTimestamp, stored[4].CardUpdatedUtc);
        Assert.Equal(untouchedBefore, stored[4].SortKey);
        Assert.Equal(["resync-requested"], Events.PublishedEventNames);
        Assert.Equal([board.BoardId], Events.ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task Reorder_WhenAlreadyInRequestedOrder_ShouldPreserveKeysAndTimestampsWithoutEvents()
    {
        // Arrange
        var board = CreateBoard().AddColumn("A").AddCard("One").AddCard("Two")
            .AddColumn("B").AddCard("Three").Build();
        var before = await PositionsAsync(DbContextForArrange);

        // Act
        var result = await ResolveService<IBoardService>().ReorderBoardAsync(board.BoardId,
            new([new(board.GetColumn("B").Id, [3]), new(board.GetColumn("A").Id, [1, 2])]), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(before, await PositionsAsync(DbContextForAssert));
        Assert.Empty(Events.PublishedEventNames);
    }

    [Theory]
    [InlineData("null-columns")]
    [InlineData("null-entry")]
    [InlineData("missing-column-id")]
    [InlineData("negative-column-id")]
    [InlineData("null-card-ids")]
    [InlineData("nonpositive-card")]
    [InlineData("duplicate-column")]
    [InlineData("duplicate-card")]
    [InlineData("missing-column")]
    [InlineData("foreign-column")]
    [InlineData("missing-card")]
    [InlineData("foreign-card")]
    [InlineData("unknown-card")]
    [InlineData("archived-card")]
    public async Task Reorder_WhenLayoutIsInvalid_ShouldRejectWithoutPartialChanges(string invalid)
    {
        // Arrange
        var board = CreateBoard().AddColumn("A").AddCard("One").AddCard("Two")
            .AddColumn("B").AddCard("Three").Build();
        var other = CreateBoard("Other").AddColumn("Foreign").AddCard("Foreign card").Build();
        var foreign = await DbContextForArrange.Cards.SingleAsync(x => x.BoardId == other.BoardId);
        foreign.BoardCardId = 500;
        DbContextForArrange.ArchivedCards.Add(new EntityArchivedCard
        {
            BoardId = board.BoardId, OriginalCardId = 600, SnapshotJson = "{}", SearchTitle = "Archived",
            SearchTagsJson = "[]", SearchTextNormalised = "ARCHIVED", ArchivedAtUtc = DateTime.UtcNow
        });
        await DbContextForArrange.SaveChangesAsync();
        var a = board.GetColumn("A").Id;
        var b = board.GetColumn("B").Id;
        IReadOnlyList<BoardCardOrder?>? columns = invalid switch
        {
            "null-columns" => null,
            "null-entry" => [null, new(b, [1, 2, 3])],
            "missing-column-id" => [new(null, [2, 1]), new(b, [3])],
            "negative-column-id" => [new(-1, [2, 1]), new(b, [3])],
            "null-card-ids" => [new(a, null), new(b, [3])],
            "nonpositive-card" => [new(a, [2, 0]), new(b, [3])],
            "duplicate-column" => [new(a, [2, 1]), new(a, [3])],
            "duplicate-card" => [new(a, [2, 1]), new(b, [1, 3])],
            "missing-column" => [new(a, [3, 2, 1])],
            "foreign-column" => [new(a, [2, 1]), new(other.GetColumn("Foreign").Id, [3])],
            "missing-card" => [new(a, [2]), new(b, [3])],
            "foreign-card" => [new(a, [2, 500]), new(b, [3])],
            "unknown-card" => [new(a, [2, 99999]), new(b, [3])],
            "archived-card" => [new(a, [2, 600]), new(b, [3])],
            _ => throw new ArgumentOutOfRangeException(nameof(invalid))
        };
        var before = await PositionsAsync(DbContextForArrange);

        // Act
        var result = await ResolveService<IBoardService>().ReorderBoardAsync(board.BoardId, new(columns), ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotEmpty(result.ValidationErrors!);
        Assert.Equal(before, await PositionsAsync(DbContextForAssert));
        Assert.Empty(Events.PublishedEventNames);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reorder_WhenBoardHasNoCards_ShouldAcceptItsCompleteColumnSet(bool hasColumn)
    {
        // Arrange
        var builder = CreateBoard();
        if (hasColumn) { builder.AddColumn("Empty"); }
        var board = builder.Build();
        BoardCardOrder?[] columns = hasColumn ? [new(board.GetColumn("Empty").Id, [])] : [];

        // Act
        var result = await ResolveService<IBoardService>().ReorderBoardAsync(board.BoardId, new(columns), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(Events.PublishedEventNames);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reorder_ShouldAllowContributorsAndRejectNonMembers(bool removeMembership)
    {
        // Arrange
        var board = CreateBoard().AddColumn("A").AddCard("One").AddCard("Two").Build();
        var member = await DbContextForArrange.BoardMembers.SingleAsync(x => x.BoardId == board.BoardId);
        member.Role = BoardMemberRole.Contributor;
        if (removeMembership) { DbContextForArrange.BoardMembers.Remove(member); }
        await DbContextForArrange.SaveChangesAsync();
        var before = await PositionsAsync(DbContextForArrange);

        // Act
        var result = await ResolveService<IBoardService>().ReorderBoardAsync(board.BoardId,
            new([new(board.GetColumn("A").Id, [2, 1])]), ActorUserId);

        // Assert
        Assert.Equal(!removeMembership, result.Success);
        if (removeMembership)
        {
            Assert.Equal(403, result.StatusCode);
            Assert.Equal(before, await PositionsAsync(DbContextForAssert));
            Assert.Empty(Events.PublishedEventNames);
        }
        else
        {
            var ids = await OrderedIdsAsync(board.GetColumn("A").Id);
            Assert.Equal([2, 1], ids);
        }
    }

    private TestBoardEvents Events => Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());

    private async Task<int[]> OrderedIdsAsync(int columnId) => await DbContextForAssert.Cards
        .Where(x => x.BoardColumnId == columnId).OrderBy(x => x.SortKey).Select(x => x.BoardCardId).ToArrayAsync();

    private static async Task<object[]> ContentsAsync(BoardOilDbContext db) => (await db.Cards.OrderBy(x => x.Id)
        .Select(x => new { x.Id, x.BoardId, x.BoardCardId, x.Title, x.Description, x.CardTypeId, x.AssignedUserId, x.SlickId, x.ExternalUrl, x.CardCreatedUtc })
        .ToArrayAsync()).Cast<object>().ToArray();

    private static async Task<object[]> PositionsAsync(BoardOilDbContext db) => (await db.Cards.OrderBy(x => x.Id)
        .Select(x => new { x.Id, x.BoardColumnId, x.SortKey, x.CardUpdatedUtc, x.UpdatedAtUtc })
        .ToArrayAsync()).Cast<object>().ToArray();
}
