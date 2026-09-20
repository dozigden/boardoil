using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Data.Abstractions.Attachment;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using BoardOil.Ef.DependencyInjection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class BoardOilApiFactoryDatabaseTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task TemplateDatabase_ShouldPersistAuditWhileIndependentReaderRemainsOpen(int cloneCount)
    {
        // Arrange: exercise the fixture's database creation/reset without starting an HTTP host.
        var directory = Directory.CreateTempSubdirectory("boardoil-template-database-");
        var databasePath = Path.Combine(directory.FullName, "test.db");
        var connectionString = $"Data Source={databasePath}";
        try
        {
            await using var factory = new BoardOilApiFactory(databasePath);
            for (var clone = 0; clone < cloneCount; clone++)
            {
                factory.ResetDatabaseFromTemplate();
            }

            await using var provider = new ServiceCollection()
                .AddBoardOilEfInfrastructure(connectionString)
                .BuildServiceProvider();
            await using var services = provider.CreateAsyncScope();
            var contexts = services.ServiceProvider.GetRequiredService<IDbContextFactory>();
            await using (var seed = contexts.CreateDbContext<BoardOilDbContext>())
            {
                seed.AppSettings.Add(new EntityAppSetting { Key = "mcp_public_base_url", Value = "https://boardoil.test" });
                await seed.SaveChangesAsync();
            }

            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT Value FROM AppSettings WHERE Key = 'mcp_public_base_url'";
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            // Keep the SELECT unfinished through the audit commit. This forces the former
            // reader/writer overlap without relying on concurrent HTTP request scheduling.
            var audit = new EntityAttachmentTransferAudit
            {
                TicketId = 1,
                Operation = AttachmentTransferOperation.Download,
                Outcome = AttachmentTransferAuditOutcome.DownloadAdmitted,
                OccurredAtUtc = DateTime.UtcNow
            };

            // Act: use the same repository and scope persistence path as download auditing.
            using (var scope = services.ServiceProvider.GetRequiredService<IDbContextScopeFactory>().Create())
            {
                services.ServiceProvider.GetRequiredService<IAttachmentTransferAuditRepository>().Add(audit);
                await scope.SaveChangesAsync();
            }

            // Assert: a separate context sees the committed audit while the reader is still open.
            Assert.False(reader.IsClosed);
            await using var verification = contexts.CreateDbContext<BoardOilDbContext>();
            var persisted = await verification.AttachmentTransferAudits.SingleAsync();
            Assert.Equal(audit.Id, persisted.Id);
            Assert.Equal(AttachmentTransferAuditOutcome.DownloadAdmitted, persisted.Outcome);
            await verification.Database.OpenConnectionAsync();
            await using var journalMode = verification.Database.GetDbConnection().CreateCommand();
            journalMode.CommandText = "PRAGMA journal_mode";
            Assert.Equal("wal", await journalMode.ExecuteScalarAsync());
        }
        finally
        {
            using var pooledConnection = new SqliteConnection(connectionString);
            SqliteConnection.ClearPool(pooledConnection);
            directory.Delete(recursive: true);
        }
    }
}
