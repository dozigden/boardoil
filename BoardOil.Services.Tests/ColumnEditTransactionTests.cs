using System.Data.Common;
using BoardOil.Abstractions;
using BoardOil.Abstractions.Column;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Column;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using BoardOil.Ef.DependencyInjection;
using BoardOil.Services.DependencyInjection;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class ColumnEditTransactionTests : IAsyncLifetime
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("boardoil-column-edit-");
    private readonly ColumnSelectionGate _gate = new();
    private readonly SaveFailure _failure = new();
    private readonly TestBoardEvents _events = new();
    private DbContextOptions<BoardOilDbContext> _readOptions = null!;
    private ServiceProvider _provider = null!;
    private FluentBoardBuilder _board = null!;
    private int _actorId;

    public async ValueTask InitializeAsync()
    {
        var connectionString = $"Data Source={Path.Combine(_directory.FullName, "test.db")};Pooling=False;Default Timeout=1";
        _readOptions = new DbContextOptionsBuilder<BoardOilDbContext>().UseSqlite(connectionString).Options;
        await using var db = new BoardOilDbContext(_readOptions);
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        var actor = new EntityUser
        {
            UserName = "actor", Email = "actor@test", NormalisedEmail = "actor@test",
            PasswordHash = "unused", Role = UserRole.Admin, IsActive = true
        };
        db.Users.Add(actor);
        await db.SaveChangesAsync();
        _actorId = actor.Id;
        _board = new FluentBoardBuilder(db, "Board", DateTime.UtcNow, _actorId)
            .AddColumn("A").AddCard("Keep card").AddColumn("B").AddColumn("C").Build();
        var writeOptions = new DbContextOptionsBuilder<BoardOilDbContext>().UseSqlite(connectionString)
            .AddInterceptors(_gate, _failure).Options;
        var services = new ServiceCollection();
        services.AddBoardOilServices();
        services.AddBoardOilEfInfrastructure(connectionString);
        services.AddSingleton<IDbContextFactory>(new TestDbContextFactory(writeOptions));
        services.AddSingleton<IBoardEvents>(_events);
        _provider = services.BuildServiceProvider();
    }

    [Theory]
    [InlineData("addColumn")]
    [InlineData("deleteColumn")]
    [InlineData("addCard")]
    public async Task Edit_ShouldPreventConcurrentWritesUntilValidatedEditIsCommitted(string change)
    {
        _gate.Enabled = true;
        var edit = Task.Run(EditAsync);
        try
        {
            await _gate.Selected.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await using var writer = new BoardOilDbContext(_readOptions);
            writer.Database.SetCommandTimeout(1);
            if (change == "addColumn")
            {
                writer.Columns.Add(new EntityBoardColumn { BoardId = _board.BoardId, Title = "Concurrent", SortKey = "00000000000000000001" });
            }
            else if (change == "deleteColumn")
            {
                writer.Columns.Remove(await writer.Columns.SingleAsync(column => column.Id == _board.GetColumn("B").Id));
            }
            else
            {
                writer.Cards.Add(new EntityBoardCard
                {
                    BoardId = _board.BoardId, BoardCardId = 100,
                    BoardColumnId = _board.GetColumn("B").Id,
                    CardTypeId = _board.GetCard("A", "Keep card").CardTypeId,
                    Title = "Concurrent card", Description = "Must not be lost",
                    SortKey = "00000000000000000001", CardCreatedUtc = DateTime.UtcNow, CardUpdatedUtc = DateTime.UtcNow
                });
            }

            var error = await Assert.ThrowsAsync<DbUpdateException>(() => writer.SaveChangesAsync());

            Assert.Equal(5, Assert.IsType<SqliteException>(error.InnerException).SqliteErrorCode);
            Assert.True(_gate.HadTransaction);
            Assert.Empty(_events.ColumnEventNames);
        }
        finally
        {
            _gate.Release.TrySetResult();
            var result = await edit.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(result.Success, result.Message);
        }
        Assert.Equal(["New", "Edited A"], (await Snapshot()).Select(column => column.Title));
        await using var stored = new BoardOilDbContext(_readOptions);
        Assert.Equal("Keep card", (await stored.Cards.SingleAsync()).Title);
    }

    [Fact]
    public async Task Edit_WhenSaveFailsAfterSqlExecution_ShouldRollBackEveryChangeAndPublishNothing()
    {
        var before = await Snapshot();
        _failure.Enabled = true;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => EditAsync());

        Assert.Equal("Simulated save failure.", error.Message);
        Assert.True(_failure.SqlExecuted);
        Assert.Equal(before, await Snapshot());
        Assert.Empty(_events.ColumnEventNames);
        await using var db = new BoardOilDbContext(_readOptions);
        Assert.Equal("Keep card", (await db.Cards.SingleAsync()).Title);
    }

    [Fact]
    public async Task Edit_ShouldPublishExistingEventsOnlyAfterWholeConfigurationIsCommitted()
    {
        _events.ColumnEventObserver = async () =>
        {
            Assert.Equal(["New", "Edited A"], (await Snapshot()).Select(column => column.Title));
        };

        var result = await EditAsync();

        Assert.True(result.Success, result.Message);
        Assert.Equal(["deleted", "deleted", "created", "updated", "reordered"], _events.ColumnEventNames);
        Assert.Equal(result.Data, Assert.Single(_events.ColumnsReorderedEvents).Columns);
    }

    private async Task<BoardOil.Contracts.Common.ApiResult<IReadOnlyList<ColumnDto>>> EditAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IColumnService>().EditColumnsAsync(_board.BoardId,
            new([new(null, "New"), new(_board.GetColumn("A").Id, "Edited A")]), _actorId);
    }

    private async Task<ColumnDto[]> Snapshot()
    {
        await using var db = new BoardOilDbContext(_readOptions);
        return await db.Columns.AsNoTracking().OrderBy(column => column.SortKey)
            .Select(column => new ColumnDto(column.Id, column.Title, column.SortKey, column.CreatedAtUtc, column.UpdatedAtUtc)).ToArrayAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        _directory.Delete(recursive: true);
    }

    private sealed class SaveFailure : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public bool SqlExecuted { get; private set; }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            if (Enabled)
            {
                SqlExecuted = true;
                throw new InvalidOperationException("Simulated save failure.");
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ColumnSelectionGate : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        public bool HadTransaction { get; private set; }
        public TaskCompletionSource Selected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (Enabled && command.CommandText.Contains("FROM \"Columns\"", StringComparison.Ordinal)
                && command.CommandText.Contains("ORDER BY", StringComparison.Ordinal))
            {
                Enabled = false;
                HadTransaction = command.Transaction is not null;
                Selected.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }
}
