using BoardOil.Abstractions.DataAccess;
using BoardOil.Ef;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Services.Tests.Infrastructure;

public sealed class TestDbContextFactory(DbContextOptions<BoardOilDbContext> options) : IDbContextFactory
{
    private readonly DbContextOptions<BoardOilDbContext> _options = options;

    public TDbContext CreateDbContext<TDbContext>() where TDbContext : DbContext
    {
        if (typeof(TDbContext) != typeof(BoardOilDbContext))
        {
            throw new InvalidOperationException($"Unsupported DbContext type: {typeof(TDbContext).Name}");
        }

        return (TDbContext)(DbContext)new BoardOilDbContext(_options);
    }
}
