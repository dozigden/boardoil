using System.Data.Common;
using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Contracts.Board;
using BoardOil.Abstractions.DataAccess;
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

public sealed class BoardReorderConcurrencyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reorder_ShouldLockCardAndColumnMembershipUntilLayoutIsCommitted(bool addColumn)
    {
        var directory = Directory.CreateTempSubdirectory("boardoil-board-reorder-");
        try
        {
            var connectionString = $"Data Source={Path.Combine(directory.FullName, "test.db")};Pooling=False;Default Timeout=1";
            var gate = new ColumnSelectionGate();
            var options = new DbContextOptionsBuilder<BoardOilDbContext>().UseSqlite(connectionString).AddInterceptors(gate).Options;
            await using var database = new BoardOilDbContext(options);
            await database.Database.EnsureCreatedAsync();
            await database.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            var actor = new EntityUser
            {
                UserName = "actor", Email = "actor@test",
                PasswordHash = "unused", Role = UserRole.Admin, IsActive = true
            };
            database.Users.Add(actor);
            await database.SaveChangesAsync();
            var board = new FluentBoardBuilder(database, "Board", DateTime.UtcNow, actor.Id)
                .AddColumn("A").AddCard("One").AddColumn("B").AddCard("Two").Build();
            var a = board.GetColumn("A").Id;
            var b = board.GetColumn("B").Id;
            var desired = new ReorderBoardRequest([new(a, [2]), new(b, [1])]);
            var services = new ServiceCollection();
            services.AddBoardOilServices();
            services.AddBoardOilEfInfrastructure(connectionString);
            services.AddSingleton<IDbContextFactory>(new TestDbContextFactory(options));
            services.AddSingleton<IBoardEvents, TestBoardEvents>();
            await using var provider = services.BuildServiceProvider();
            gate.Enabled = true;
            var reorder = Task.Run(async () =>
            {
                await using var scope = provider.CreateAsyncScope();
                return await scope.ServiceProvider.GetRequiredService<IBoardService>()
                    .ReorderBoardAsync(board.BoardId, desired, actor.Id);
            });

            try
            {
                await gate.Selected.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await using var writer = new SqliteConnection(connectionString);
                await writer.OpenAsync();
                await using var command = writer.CreateCommand();
                if (addColumn)
                {
                    command.CommandText = "INSERT INTO Columns (BoardId, Title, SortKey, CreatedAtUtc, UpdatedAtUtc) "
                        + "VALUES ($board, 'New', '00000000000000000001', '2026-10-19', '2026-10-19')";
                    command.Parameters.AddWithValue("$board", board.BoardId);
                }
                else
                {
                    command.CommandText = "DELETE FROM Cards WHERE BoardId = $board AND BoardCardId = 1";
                    command.Parameters.AddWithValue("$board", board.BoardId);
                }

                var error = await Assert.ThrowsAsync<SqliteException>(() => command.ExecuteNonQueryAsync());

                Assert.Equal(5, error.SqliteErrorCode);
                Assert.True(gate.HadTransaction);
            }
            finally
            {
                gate.Release.TrySetResult();
                var result = await reorder.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.True(result.Success, result.Message);
            }

            var aIds = await database.Cards.AsNoTracking().Where(card => card.BoardColumnId == a)
                .OrderBy(card => card.SortKey).Select(card => card.BoardCardId).ToArrayAsync();
            var bIds = await database.Cards.AsNoTracking().Where(card => card.BoardColumnId == b)
                .OrderBy(card => card.SortKey).Select(card => card.BoardCardId).ToArrayAsync();
            Assert.Equal([2], aIds);
            Assert.Equal([1], bIds);
        }
        finally
        {
            directory.Delete(recursive: true);
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
