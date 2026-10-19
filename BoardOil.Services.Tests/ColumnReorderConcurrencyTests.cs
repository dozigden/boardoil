using System.Data.Common;
using BoardOil.Abstractions;
using BoardOil.Abstractions.Column;
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

public sealed class ColumnReorderConcurrencyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reorder_ShouldLockColumnMembershipUntilOrderIsCommitted(bool addColumn)
    {
        var directory = Directory.CreateTempSubdirectory("boardoil-column-reorder-");
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
                UserName = "actor", Email = "actor@test", NormalisedEmail = "actor@test",
                PasswordHash = "unused", Role = UserRole.Admin, IsActive = true
            };
            database.Users.Add(actor);
            await database.SaveChangesAsync();
            var board = new FluentBoardBuilder(database, "Board", DateTime.UtcNow, actor.Id)
                .AddColumn("A").AddColumn("B").Build();
            int[] desired = [board.GetColumn("B").Id, board.GetColumn("A").Id];
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
                return await scope.ServiceProvider.GetRequiredService<IColumnService>()
                    .ReorderColumnsAsync(board.BoardId, new(desired), actor.Id);
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
                    command.CommandText = "DELETE FROM Columns WHERE Id = $id";
                    command.Parameters.AddWithValue("$id", desired[0]);
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

            Assert.Equal(desired, await database.Columns.AsNoTracking().OrderBy(column => column.SortKey)
                .Select(column => column.Id).ToArrayAsync());
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
