using BoardOil.Ef.Context;
using BoardOil.Ef.Diagnostics;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class DatabaseInfoTests
{
    [Theory]
    [InlineData("delete")]
    [InlineData("wal")]
    public async Task Get_ShouldReportCurrentDatabaseWithoutChangingItsMode(string mode)
    {
        var directory = Directory.CreateTempSubdirectory("boardoil-db-info-");
        var path = Path.Combine(directory.FullName, "test.db");
        var connectionString = $"Data Source={path};Pooling=False";
        try
        {
            await using (var setup = new SqliteConnection(connectionString))
            {
                await setup.OpenAsync();
                await using var command = setup.CreateCommand();
                command.CommandText = $"PRAGMA journal_mode={mode}; CREATE TABLE sample (id INTEGER PRIMARY KEY);";
                await command.ExecuteNonQueryAsync();
            }
            var provider = new SqliteDatabaseInfoProvider(new BoardOilDbContextFactory(connectionString));

            var info = await provider.GetAsync(TestContext.Current.CancellationToken);

            Assert.Equal("SQLite", info.Provider);
            Assert.Matches(@"^\d+\.\d+\.\d+", info.Version);
            Assert.Equal(mode, info.JournalMode);
            Assert.Equal(new FileInfo(path).Length, info.SizeBytes);
            await using var verify = new SqliteConnection(connectionString);
            await verify.OpenAsync();
            await using var query = verify.CreateCommand();
            query.CommandText = "PRAGMA journal_mode;";
            Assert.Equal(mode, await query.ExecuteScalarAsync());
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
