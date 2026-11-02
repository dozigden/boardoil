using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Support;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Diagnostics;

public sealed class SqliteDatabaseInfoProvider(IDbContextFactory contextFactory) : IDatabaseInfoProvider
{
    public async Task<DatabaseInfoDto> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var context = contextFactory.CreateDbContext<BoardOilDbContext>();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT sqlite_version(), journal_mode, page_count * page_size
            FROM pragma_journal_mode(), pragma_page_count(), pragma_page_size();
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new DatabaseInfoDto("SQLite", reader.GetString(0), reader.GetString(1), reader.GetInt64(2));
    }
}
