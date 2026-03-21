using Microsoft.EntityFrameworkCore;

namespace BoardOil.Abstractions.DataAccess;

public interface IDbContextFactory
{
    TDbContext CreateDbContext<TDbContext>() where TDbContext : DbContext;
}
