using BoardOil.Ef;
using BoardOil.Ef.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests.Infrastructure;

public abstract class TestBaseDb : IAsyncLifetime
{
    private SqliteTestHarness? _harness;
    private BoardOilDbContext? _dbContextForArrange;
    private BoardOilDbContext? _dbContextForAssert;
    private readonly List<BoardOilDbContext> _actDbContexts = [];

    protected BoardOilDbContext DbContextForArrange =>
        _dbContextForArrange ??= Harness.CreateDbContext();

    protected BoardOilDbContext DbContextForAssert =>
        _dbContextForAssert ??= Harness.CreateDbContext();

    protected BoardOilDbContext CreateDbContextForAct()
    {
        var dbContext = Harness.CreateDbContext();
        _actDbContexts.Add(dbContext);
        return dbContext;
    }

    private SqliteTestHarness Harness =>
        _harness ?? throw new InvalidOperationException("Test harness has not been initialized.");

    public async Task InitializeAsync()
    {
        _harness = await SqliteTestHarness.CreateAsync();
    }

    public async Task DisposeAsync()
    {
        foreach (var actDbContext in _actDbContexts)
        {
            await actDbContext.DisposeAsync();
        }

        if (_dbContextForAssert is not null)
        {
            await _dbContextForAssert.DisposeAsync();
        }

        if (_dbContextForArrange is not null)
        {
            await _dbContextForArrange.DisposeAsync();
        }

        if (_harness is not null)
        {
            await _harness.DisposeAsync();
        }
    }

    protected async Task<(int TodoColumnId, int DoingColumnId)> SeedTwoColumnBoardAsync()
    {
        var board = await SeedDefaultBoardAsync();
        var columns = await SeedColumnsAsync(board.Id, "Todo", "Doing");
        return (columns[0].Id, columns[1].Id);
    }

    protected async Task<int> SeedSingleCardAsync(int columnId, string title, string description)
    {
        var cards = await SeedCardsAsync(
            columnId,
            (title, description, TestSeedData.OrderedSortKeys[2]));
        return cards[0].Id;
    }

    protected Task<Board> SeedDefaultBoardAsync(string name = "BoardOil") =>
        TestSeedData.SeedDefaultBoardAsync(DbContextForArrange, name);

    protected Task<IReadOnlyList<BoardColumn>> SeedColumnsAsync(int boardId, params string[] titles) =>
        TestSeedData.SeedColumnsAsync(DbContextForArrange, boardId, titles);

    protected Task<IReadOnlyList<BoardCard>> SeedCardsAsync(
        int columnId,
        params (string Title, string Description, string SortKey)[] cards) =>
        TestSeedData.SeedCardsAsync(DbContextForArrange, columnId, cards);

    protected static async Task<List<string>> GetOrderedTitlesAsync(BoardOilDbContext db, int columnId) =>
        await db.Cards
            .Where(x => x.BoardColumnId == columnId)
            .OrderBy(x => x.SortKey)
            .Select(x => x.Title)
            .ToListAsync();
}
