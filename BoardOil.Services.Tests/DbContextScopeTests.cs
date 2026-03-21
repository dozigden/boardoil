using BoardOil.Abstractions.DataAccess;
using BoardOil.Ef;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Ef.Repositories;
using BoardOil.Ef.Scope;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using BoardEntity = BoardOil.Persistence.Abstractions.Entities.EntityBoard;

namespace BoardOil.Services.Tests;

public sealed class DbContextScopeTests : IAsyncLifetime
{
    private SqliteTestHarness _harness = null!;
    private IDbContextScopeFactory _scopeFactory = null!;
    private IAmbientDbContextLocator _locator = null!;

    public async Task InitializeAsync()
    {
        _harness = await SqliteTestHarness.CreateAsync();
        var dbContextFactory = new TestDbContextFactory(_harness.Options);
        _scopeFactory = new DbContextScopeFactory(dbContextFactory);
        _locator = new AmbientDbContextLocator();
    }

    public async Task DisposeAsync()
    {
        await _harness.DisposeAsync();
    }

    [Fact]
    public async Task RepositoryCallWithoutAmbientScope_ShouldFailFast()
    {
        var repository = new BoardRepository(_locator);

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => repository.AnyBoardAsync());
        Assert.Contains("No ambient DbContext", exception.Message);
    }

    [Fact]
    public void CreateReadOnly_ShouldDisableAutoDetectChanges()
    {
        using var scope = _scopeFactory.CreateReadOnly();

        var dbContext = _locator.Get<BoardOilDbContext>();

        Assert.NotNull(dbContext);
        Assert.False(dbContext!.ChangeTracker.AutoDetectChangesEnabled);
    }

    [Fact]
    public async Task SaveChangesCalledTwiceInOneScope_ShouldThrow()
    {
        using var scope = _scopeFactory.Create();

        var dbContext = _locator.Get<BoardOilDbContext>();
        Assert.NotNull(dbContext);

        dbContext!.Boards.Add(new BoardEntity
        {
            Name = "Board A",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await scope.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.SaveChangesAsync());
    }

    [Fact]
    public void NestedScopes_ShouldJoinAmbientByDefaultAndAllowForceCreateNew()
    {
        using var outer = _scopeFactory.Create();
        var outerContext = _locator.Get<BoardOilDbContext>();

        using var joined = _scopeFactory.Create();
        var joinedContext = _locator.Get<BoardOilDbContext>();

        using var forced = _scopeFactory.Create(DbContextScopeOption.ForceCreateNew);
        var forcedContext = _locator.Get<BoardOilDbContext>();

        Assert.NotNull(outerContext);
        Assert.Same(outerContext, joinedContext);
        Assert.NotSame(outerContext, forcedContext);
    }

    [Fact]
    public async Task ExplicitTransaction_ShouldAllowMultipleIntermediateSaves()
    {
        using var scope = _scopeFactory.Create();

        await scope.Transaction(async (transactionScope, transaction) =>
        {
            var dbContext = _locator.Get<BoardOilDbContext>();
            Assert.NotNull(dbContext);

            dbContext!.Boards.Add(new BoardEntity
            {
                Name = "Board 1",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await transactionScope.SaveChangesAsync();

            dbContext.Boards.Add(new BoardEntity
            {
                Name = "Board 2",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await transactionScope.SaveChangesAsync();

            await transaction.CommitAsync();
        });

        await using var assertDbContext = _harness.CreateDbContext();
        var boards = await assertDbContext.Boards.OrderBy(x => x.Name).ToListAsync();

        Assert.Equal(2, boards.Count);
        Assert.Equal("Board 1", boards[0].Name);
        Assert.Equal("Board 2", boards[1].Name);
    }
}
