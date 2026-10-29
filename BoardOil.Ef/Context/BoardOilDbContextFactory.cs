using BoardOil.Abstractions.DataAccess;
using BoardOil.Ef.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Context;

public sealed class BoardOilDbContextFactory(string connectionString, SqliteLockDiagnostics? diagnostics = null) : IDbContextFactory
{
    private readonly string _connectionString = connectionString;

    public TDbContext CreateDbContext<TDbContext>() where TDbContext : DbContext
    {
        if (typeof(TDbContext) != typeof(BoardOilDbContext))
        {
            throw new InvalidOperationException($"Unsupported DbContext type: {typeof(TDbContext).Name}");
        }

        var options = new DbContextOptionsBuilder<BoardOilDbContext>()
            .UseSqlite(_connectionString)
            .UseOpenIddict()
            .Options;

        return (TDbContext)(DbContext)new BoardOilDbContext(options, diagnostics);
    }
}
