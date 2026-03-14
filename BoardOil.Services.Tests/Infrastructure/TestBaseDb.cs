using BoardOil.Ef;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests.Infrastructure;

public abstract class TestBaseDb : IAsyncLifetime
{
    private static readonly DateTime FixedNow = new(2026, 3, 14, 12, 0, 0, DateTimeKind.Utc);
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

    protected FluentBoardBuilder CreateBoard(string name = "BoardOil") =>
        new(DbContextForArrange, name, FixedNow);

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

    protected static async Task<List<string>> GetOrderedTitlesAsync(BoardOilDbContext db, int columnId) =>
        await db.Cards
            .Where(x => x.BoardColumnId == columnId)
            .OrderBy(x => x.SortKey)
            .Select(x => x.Title)
            .ToListAsync();
}
