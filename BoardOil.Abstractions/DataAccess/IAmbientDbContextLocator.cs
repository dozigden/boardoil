using Microsoft.EntityFrameworkCore;

namespace BoardOil.Abstractions.DataAccess;

public interface IAmbientDbContextLocator
{
    TDbContext? Get<TDbContext>() where TDbContext : DbContext;
}
