using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using BoardOil.Ef.Card;
using BoardOil.Ef.Context;
using BoardOil.Ef.Scope;
using BoardOil.Services.Card;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class BoardCardIdAllocatorTests
{
    [Fact]
    public async Task AllocateNextAsync_WithConcurrentConnections_ShouldAllocateDistinctIds()
    {
        // Arrange
        var databasePath = CreateDatabasePath();
        var connectionString = $"Data Source={databasePath};Default Timeout=30;Pooling=False";

        try
        {
            var boardId = await CreateBoardAsync(connectionString);
            const int allocationCount = 12;
            var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var allocationTasks = Enumerable.Range(0, allocationCount)
                .Select(async _ =>
                {
                    await startGate.Task;
                    return await AllocateAsync(connectionString, boardId);
                })
                .ToList();

            // Act
            startGate.SetResult();
            var allocatedIds = await Task.WhenAll(allocationTasks);

            // Assert
            Assert.Equal(
                Enumerable.Range(1, allocationCount),
                allocatedIds.OrderBy(x => x));

            await using var assertContext = CreateDbContext(connectionString);
            var nextCardId = await assertContext.BoardCardIdSequences
                .Where(x => x.BoardId == boardId)
                .Select(x => x.NextCardId)
                .SingleAsync();
            Assert.Equal(allocationCount + 1, nextCardId);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task AllocateNextAsync_ForDifferentBoards_ShouldAllocateSameBoardScopedId()
    {
        // Arrange
        var databasePath = CreateDatabasePath();
        var connectionString = $"Data Source={databasePath};Default Timeout=30;Pooling=False";

        try
        {
            var firstBoardId = await CreateBoardAsync(connectionString);
            var secondBoardId = await CreateBoardAsync(connectionString);

            // Act
            var firstBoardCardId = await AllocateAsync(connectionString, firstBoardId);
            var secondBoardCardId = await AllocateAsync(connectionString, secondBoardId);

            // Assert
            Assert.Equal(1, firstBoardCardId);
            Assert.Equal(1, secondBoardCardId);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task AllocateNextAsync_WhenCardInsertTransactionRollsBack_ShouldRollbackCardAndSequence()
    {
        // Arrange
        var databasePath = CreateDatabasePath();
        var connectionString = $"Data Source={databasePath};Default Timeout=30;Pooling=False";

        try
        {
            var boardId = await CreateBoardAsync(connectionString);
            var rolledBackId = await AllocateWithoutCommitAsync(connectionString, boardId);

            // Act
            var committedId = await AllocateAsync(connectionString, boardId);

            // Assert
            Assert.Equal(1, rolledBackId);
            Assert.Equal(1, committedId);
            await using var assertContext = CreateDbContext(connectionString);
            Assert.Empty(await assertContext.Cards.ToListAsync());
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task AllocateNextAsync_WithoutExplicitTransaction_ShouldFailWithoutAdvancingSequence()
    {
        // Arrange
        var databasePath = CreateDatabasePath();
        var connectionString = $"Data Source={databasePath};Default Timeout=30;Pooling=False";

        try
        {
            var boardId = await CreateBoardAsync(connectionString);
            var dbContextFactory = new BoardOilDbContextFactory(connectionString);
            var scopeFactory = new DbContextScopeFactory(dbContextFactory);
            var allocator = new BoardCardIdAllocator(new AmbientDbContextLocator());
            using var scope = scopeFactory.Create();

            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => allocator.AllocateNextAsync(boardId));

            // Assert
            Assert.Equal("Board card ID allocation requires an explicit database transaction.", exception.Message);
            await scope.SaveChangesAsync();

            await using var assertContext = CreateDbContext(connectionString);
            var nextCardId = await assertContext.BoardCardIdSequences
                .Where(x => x.BoardId == boardId)
                .Select(x => x.NextCardId)
                .SingleAsync();
            Assert.Equal(1, nextCardId);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    private static async Task<int> CreateBoardAsync(string connectionString)
    {
        await using var dbContext = CreateDbContext(connectionString);
        await dbContext.Database.EnsureCreatedAsync();

        var board = new EntityBoard
        {
            Name = $"Board {Guid.NewGuid():N}",
            CardIdSequence = new EntityBoardCardIdSequence(),
        };
        board.CardTypes.Add(CardTypeDefaults.CreateSystemForBoard(board, DateTime.UtcNow));
        board.Columns.Add(new EntityBoardColumn
        {
            Title = "Todo",
            SortKey = "A",
        });
        dbContext.Boards.Add(board);
        await dbContext.SaveChangesAsync();
        return board.Id;
    }

    private static async Task<int> AllocateAsync(string connectionString, int boardId)
    {
        var dbContextFactory = new BoardOilDbContextFactory(connectionString);
        var scopeFactory = new DbContextScopeFactory(dbContextFactory);
        var allocator = new BoardCardIdAllocator(new AmbientDbContextLocator());
        using var scope = scopeFactory.Create();

        var allocatedId = 0;
        await scope.Transaction(async (_, transaction) =>
        {
            allocatedId = await allocator.AllocateNextAsync(boardId);
            await transaction.CommitAsync();
        });
        return allocatedId;
    }

    private static async Task<int> AllocateWithoutCommitAsync(string connectionString, int boardId)
    {
        var dbContextFactory = new BoardOilDbContextFactory(connectionString);
        var scopeFactory = new DbContextScopeFactory(dbContextFactory);
        var ambientDbContextLocator = new AmbientDbContextLocator();
        var allocator = new BoardCardIdAllocator(ambientDbContextLocator);
        using var scope = scopeFactory.Create();

        var allocatedId = 0;
        await scope.Transaction(async (transactionScope, _) =>
        {
            allocatedId = await allocator.AllocateNextAsync(boardId);
            var dbContext = ambientDbContextLocator.Get<BoardOilDbContext>()!;
            var columnId = await dbContext.Columns
                .Where(x => x.BoardId == boardId)
                .Select(x => x.Id)
                .SingleAsync();
            var cardTypeId = await dbContext.CardTypes
                .Where(x => x.BoardId == boardId)
                .Select(x => x.Id)
                .SingleAsync();
            dbContext.Cards.Add(new EntityBoardCard
            {
                BoardId = boardId,
                BoardCardId = allocatedId,
                BoardColumnId = columnId,
                CardTypeId = cardTypeId,
                Title = "Rolled back card",
                Description = "",
                SortKey = "A",
            });
            await transactionScope.SaveChangesAsync();
        });
        return allocatedId;
    }

    private static BoardOilDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<BoardOilDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new BoardOilDbContext(options);
    }

    private static string CreateDatabasePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "boardoil-board-card-id-tests");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{Guid.NewGuid():N}.db");
    }
}
