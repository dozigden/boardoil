using BoardOil.Abstractions;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.Column;
using BoardOil.Contracts.Column;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class ColumnEditServiceTests : TestBaseDb
{
    private IColumnService Service => ResolveService<IColumnService>();
    private TestBoardEvents Events => Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());

    [Fact]
    public async Task Edit_ShouldApplyMixedChangesAndPreserveRetainedCardsAndOtherBoards()
    {
        var board = CreateBoard().AddColumn("A").AddCard("Keep", "Content").AddColumn("B").AddColumn("C").Build();
        CreateBoard("Other").AddColumn("Other column").Build();
        var before = await StoredColumns();
        var card = await DbContextForAssert.Cards.AsNoTracking().SingleAsync();
        var a = board.GetColumn("A").Id;
        var b = board.GetColumn("B").Id;
        var c = board.GetColumn("C").Id;

        var result = await Service.EditColumnsAsync(board.BoardId,
            new([new(c, " C edited "), new(null, "New"), new(a, "A")]), ActorUserId);

        Assert.True(result.Success, result.Message);
        var columns = result.Data!;
        Assert.Equal(["C edited", "New", "A"], columns.Select(column => column.Title));
        Assert.Equal(c, columns[0].Id);
        Assert.Equal(a, columns[2].Id);
        Assert.DoesNotContain(columns[1].Id, before.Select(column => column.Id));
        Assert.DoesNotContain(await StoredColumns(), column => column.Id == b);
        Assert.Equal(columns, (await StoredColumns()).Where(column => columns.Any(item => item.Id == column.Id)).OrderBy(column => column.SortKey));
        Assert.All(columns, column => Assert.DoesNotContain(column.SortKey, before.Select(item => item.SortKey)));
        var storedCard = await DbContextForAssert.Cards.AsNoTracking().SingleAsync();
        Assert.Equal((card.Id, card.BoardColumnId, card.SortKey, card.Description, card.CardUpdatedUtc),
            (storedCard.Id, storedCard.BoardColumnId, storedCard.SortKey, storedCard.Description, storedCard.CardUpdatedUtc));
        Assert.Equal(before.Last(), (await StoredColumns()).Single(column => column.Id == before.Last().Id));
        Assert.Equal(["deleted", "created", "updated", "reordered"], Events.ColumnEventNames);
        Assert.Equal(columns, Assert.Single(Events.ColumnsReorderedEvents).Columns);
    }

    public static TheoryData<string, string> InvalidDefinitions => new()
    {
        { "missing", "columns" }, { "nullEntry", "columns[0]" },
        { "zero", "columns[0].id" }, { "negative", "columns[0].id" },
        { "duplicate", "columns[1].id" }, { "unknown", "columns[0].id" },
        { "foreign", "columns[0].id" }, { "missingTitle", "columns[0].title" },
        { "blank", "columns[0].title" }, { "long", "columns[0].title" },
        { "characters", "columns[0].title" }
    };

    [Theory]
    [MemberData(nameof(InvalidDefinitions))]
    public async Task Edit_WithInvalidDefinition_ShouldRejectWholeEdit(string invalid, string field)
    {
        var board = CreateBoard().AddColumn("A").AddColumn("B").Build();
        var foreign = CreateBoard("Other").AddColumn("Foreign").Build();
        var id = board.GetColumn("A").Id;
        IReadOnlyList<ColumnEditEntry?>? desired = invalid switch
        {
            "missing" => null,
            "nullEntry" => [null],
            "zero" => [new(0, "A")],
            "negative" => [new(-1, "A")],
            "duplicate" => [new(id, "A"), new(id, "Duplicate")],
            "unknown" => [new(int.MaxValue, "New")],
            "foreign" => [new(foreign.GetColumn("Foreign").Id, "Foreign")],
            "missingTitle" => [new(id, null)],
            "blank" => [new(id, "  ")],
            "long" => [new(id, new string('A', 201))],
            "characters" => [new(id, "bad@title")],
            _ => throw new ArgumentOutOfRangeException(nameof(invalid))
        };
        var before = await StoredColumns();

        var result = await Service.EditColumnsAsync(board.BoardId, new(desired), ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains(field, result.ValidationErrors!.Keys);
        if (invalid == "unknown")
        {
            Assert.Contains("To create a column, omit its ID.", result.Message);
        }
        Assert.Equal(before, await StoredColumns());
        Assert.Empty(Events.ColumnEventNames);
    }

    [Fact]
    public async Task Edit_WhenOmittedColumnHasCards_ShouldPreserveAllColumnsCardsAndComments()
    {
        var board = CreateBoard().AddColumn("Occupied").AddCard("Keep", "Content").AddColumn("Empty").Build();
        var card = board.GetCard("Occupied", "Keep");
        DbContextForArrange.CardComments.Add(new EntityCardComment { CardId = card.Id, AuthorUserId = ActorUserId, Text = "Keep comment" });
        await DbContextForArrange.SaveChangesAsync();
        var before = await StoredColumns();

        var result = await Service.EditColumnsAsync(board.BoardId,
            new([new(null, "New"), new(board.GetColumn("Empty").Id, "Edited")]), ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains($"Column {board.GetColumn("Occupied").Id} ('Occupied')", result.Message);
        Assert.Contains("columns", result.ValidationErrors!.Keys);
        Assert.Equal(before, await StoredColumns());
        var storedCard = await DbContextForAssert.Cards.AsNoTracking().SingleAsync();
        Assert.Equal((card.Id, card.BoardColumnId, card.Description), (storedCard.Id, storedCard.BoardColumnId, storedCard.Description));
        Assert.Equal("Keep comment", (await DbContextForAssert.CardComments.SingleAsync()).Text);
        Assert.Empty(Events.ColumnEventNames);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Edit_WithUnchangedConfiguration_ShouldPreserveKeysTimestampsAndEvents(bool empty)
    {
        var builder = CreateBoard();
        if (!empty) builder.AddColumn("A").AddColumn("B");
        var board = builder.Build();
        var before = await StoredColumns();

        var result = await Service.EditColumnsAsync(board.BoardId,
            new(before.Select(column => new ColumnEditEntry(column.Id, $" {column.Title} ")).ToArray()), ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(before, result.Data);
        Assert.Equal(before, await StoredColumns());
        Assert.Empty(Events.ColumnEventNames);
    }

    [Fact]
    public async Task Edit_WithTitleChangeOnly_ShouldKeepKeysAndPublishOnlyChangedColumn()
    {
        var board = CreateBoard().AddColumn("A").AddColumn("B").Build();
        var before = await StoredColumns();

        var result = await Service.EditColumnsAsync(board.BoardId,
            new([new(before[0].Id, "Renamed"), new(before[1].Id, "B")]), ActorUserId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(before.Select(column => column.SortKey), result.Data.Select(column => column.SortKey));
        Assert.Equal(before[1], result.Data[1]);
        Assert.Equal(["updated"], Events.ColumnEventNames);
        Assert.Equal("Renamed", Assert.Single(Events.ColumnUpdatedEvents).Column.Title);
    }

    [Fact]
    public async Task Edit_WithNewEntries_ShouldTrimTitlesAndAllowDuplicatesAndReturnAllocatedIds()
    {
        var board = CreateBoard().Build();

        var result = await Service.EditColumnsAsync(board.BoardId, new([new(null, " Same "), new(null, "Same")]), ActorUserId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(["Same", "Same"], result.Data.Select(column => column.Title));
        Assert.Equal(2, result.Data.Select(column => column.Id).Distinct().Count());
        Assert.All(result.Data, column => Assert.True(column.Id > 0));
        Assert.Equal(["created", "created", "reordered"], Events.ColumnEventNames);
    }

    [Fact]
    public async Task Edit_WithEmptyList_ShouldRemoveEmptyColumnsAndPreserveArchives()
    {
        var board = CreateBoard().AddColumn("Archived").AddCard("Keep archive").AddColumn("Empty").Build();
        var archived = await ResolveService<ICardArchiveService>().ArchiveCardAsync(board.BoardId, board.GetCard("Archived", "Keep archive").BoardCardId, ActorUserId);
        Assert.True(archived.Success, archived.Message);
        var snapshot = (await DbContextForAssert.ArchivedCards.AsNoTracking().SingleAsync()).SnapshotJson;

        var result = await Service.EditColumnsAsync(board.BoardId, new([]), ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Empty(result.Data!);
        Assert.Empty(await StoredColumns());
        Assert.Equal(snapshot, (await DbContextForAssert.ArchivedCards.AsNoTracking().SingleAsync()).SnapshotJson);
        Assert.Equal(["deleted", "deleted", "reordered"], Events.ColumnEventNames);
    }

    [Fact]
    public async Task Edit_AfterNewColumnWasAdded_ShouldApplyToCurrentState()
    {
        var board = CreateBoard().AddColumn("A").Build();
        var staleId = board.GetColumn("A").Id;
        var added = await Service.CreateColumnAsync(board.BoardId, new("Added later"), ActorUserId);
        Assert.True(added.Success);

        var result = await Service.EditColumnsAsync(board.BoardId, new([new(staleId, "A")]), ActorUserId);

        Assert.True(result.Success);
        Assert.Equal(staleId, Assert.Single(await StoredColumns()).Id);
        Assert.DoesNotContain(result.Data!, column => column.Id == added.Data!.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Edit_WithoutOwnership_ShouldReturnForbiddenWithoutChanges(bool removeMembership)
    {
        var board = CreateBoard().AddColumn("A").Build();
        var membership = await DbContextForArrange.BoardMembers.SingleAsync();
        if (removeMembership) DbContextForArrange.BoardMembers.Remove(membership);
        else membership.Role = BoardMemberRole.Contributor;
        await DbContextForArrange.SaveChangesAsync();
        var before = await StoredColumns();

        var result = await Service.EditColumnsAsync(board.BoardId, new([]), ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(before, await StoredColumns());
        Assert.Empty(Events.ColumnEventNames);
    }

    [Fact]
    public async Task Edit_WithMissingBoard_ShouldReturnNotFound()
    {
        var result = await Service.EditColumnsAsync(int.MaxValue, new([]), ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Empty(Events.ColumnEventNames);
    }

    private Task<ColumnDto[]> StoredColumns() => DbContextForAssert.Columns.AsNoTracking().OrderBy(column => column.Id)
        .Select(column => new ColumnDto(column.Id, column.Title, column.SortKey, column.CreatedAtUtc, column.UpdatedAtUtc)).ToArrayAsync();
}
