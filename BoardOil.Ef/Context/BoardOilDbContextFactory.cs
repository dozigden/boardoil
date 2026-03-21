using BoardOil.Abstractions.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Context;

public sealed class BoardOilDbContextFactory(string connectionString) : IDbContextFactory
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
            .Options;

        return (TDbContext)(DbContext)new BoardOilDbContext(options);
    }
}
