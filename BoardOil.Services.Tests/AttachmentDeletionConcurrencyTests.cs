using System.Data.Common;
using BoardOil.Abstractions;
using BoardOil.Abstractions.Attachment;
using BoardOil.Abstractions.Board;
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

public sealed class AttachmentDeletionConcurrencyTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Deletion_ShouldLockBeforeSelectingAttachments(bool deleteBoard, bool deleteAttachment)
    {
        var directory = Directory.CreateTempSubdirectory("boardoil-attachment-delete-");
        try
        {
            var connectionString = $"Data Source={Path.Combine(directory.FullName, "test.db")};Pooling=False;Default Timeout=1";
            var gate = new AttachmentSelectionGate();
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
            var board = new FluentBoardBuilder(database, "Source", DateTime.UtcNow, actor.Id)
                .AddColumn("Source").AddCard("Card").AddColumn("Destination").Build();
            var destinationBoard = new FluentBoardBuilder(database, "Destination board", DateTime.UtcNow, actor.Id)
                .AddColumn("Destination").Build();
            var card = board.GetCard("Source", "Card");
            var services = new ServiceCollection();
            services.AddBoardOilServices();
            services.AddBoardOilEfInfrastructure(connectionString);
            services.AddSingleton<IDbContextFactory>(new TestDbContextFactory(options));
            services.AddSingleton<IBoardEvents, TestBoardEvents>();
            services.AddSingleton(new AttachmentStorageOptions { RootPath = Path.Combine(directory.FullName, "attachments") });
            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var uploaded = await scope.ServiceProvider.GetRequiredService<ICardAttachmentService>()
                .UploadAsync(board.BoardId, card.BoardCardId, actor.Id, "file.bin", null, new MemoryStream([1]));
            Assert.True(uploaded.Success, uploaded.Message);
            gate.Enabled = true;
            var deletion = Task.Run(async () =>
            {
                await using var deletionScope = provider.CreateAsyncScope();
                if (deleteAttachment)
                {
                    return await deletionScope.ServiceProvider.GetRequiredService<ICardAttachmentService>()
                        .DeleteFromBoardAsync(board.BoardId, uploaded.Data!.Id, actor.Id);
                }
                if (deleteBoard)
                {
                    return await deletionScope.ServiceProvider.GetRequiredService<IBoardService>().DeleteBoardAsync(board.BoardId, actor.Id);
                }
                return await deletionScope.ServiceProvider.GetRequiredService<IColumnService>()
                    .DeleteColumnAsync(board.BoardId, board.GetColumn("Source").Id, actor.Id);
            });

            try
            {
                await gate.Selected.Task.WaitAsync(TimeSpan.FromSeconds(10));
                // WAL allows this independent writer past a plain SELECT, but not the
                // explicit deletion transaction. This exercises the former race window.
                await using var mover = new SqliteConnection(connectionString);
                await mover.OpenAsync();
                await using var command = mover.CreateCommand();
                command.CommandText = "UPDATE Cards SET BoardId = $board, BoardColumnId = $column WHERE Id = $card";
                command.Parameters.AddWithValue("$board", deleteBoard || deleteAttachment ? destinationBoard.BoardId : board.BoardId);
                command.Parameters.AddWithValue("$column", deleteBoard || deleteAttachment ? destinationBoard.GetColumn("Destination").Id : board.GetColumn("Destination").Id);
                command.Parameters.AddWithValue("$card", card.Id);

                var exception = await Assert.ThrowsAsync<SqliteException>(() => command.ExecuteNonQueryAsync());

                Assert.Equal(5, exception.SqliteErrorCode);
                Assert.True(gate.HadTransaction);
            }
            finally
            {
                gate.Release.TrySetResult();
                var result = await deletion.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.True(result.Success, result.Message);
            }
            Assert.Equal(deleteAttachment, await database.Cards.AsNoTracking().AnyAsync(x => x.Id == card.Id));
            Assert.Empty(await database.CardAttachments.AsNoTracking().ToListAsync());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private sealed class AttachmentSelectionGate : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        public bool HadTransaction { get; private set; }
        public TaskCompletionSource Selected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (Enabled && command.CommandText.Contains("FROM \"CardAttachments\"", StringComparison.Ordinal))
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
