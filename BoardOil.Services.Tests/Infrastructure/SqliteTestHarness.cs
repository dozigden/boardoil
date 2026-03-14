using BoardOil.Ef;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Services.Tests.Infrastructure;

public sealed class SqliteTestHarness : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    public DbContextOptions<BoardOilDbContext> Options { get; }

    private SqliteTestHarness(SqliteConnection connection, DbContextOptions<BoardOilDbContext> options)
    {
        _connection = connection;
        Options = options;
    }

    public static async Task<SqliteTestHarness> CreateAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BoardOilDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new BoardOilDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
        }

        return new SqliteTestHarness(connection, options);
    }

    public BoardOilDbContext CreateDbContext() => new(Options);

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
