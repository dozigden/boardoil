using BoardOil.Ef;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Services.Tests.Infrastructure;

public sealed class SqliteTestHarness : IAsyncDisposable
{
    private static readonly object DatabaseTemplateLock = new();
    private static readonly Lazy<SqliteConnection> DatabaseTemplate = new(CreateDatabaseTemplate);

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

        lock (DatabaseTemplateLock)
        {
            DatabaseTemplate.Value.BackupDatabase(connection);
        }

        var options = new DbContextOptionsBuilder<BoardOilDbContext>()
            .UseSqlite(connection)
            .Options;

        return new SqliteTestHarness(connection, options);
    }

    private static SqliteConnection CreateDatabaseTemplate()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        try
        {
            var options = new DbContextOptionsBuilder<BoardOilDbContext>()
                .UseSqlite(connection)
                .Options;
            using var db = new BoardOilDbContext(options);
            db.Database.EnsureCreated();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    public BoardOilDbContext CreateDbContext() => new(Options);

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
