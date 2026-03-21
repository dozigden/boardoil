using BoardOil.Abstractions.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Scope;

public sealed class AmbientDbContextLocator : IAmbientDbContextLocator
{
    public TDbContext? Get<TDbContext>() where TDbContext : DbContext
    {
        var ambientScope = DbContextScope.GetAmbientScope();
        return ambientScope == null ? null : ambientScope.DbContexts.Get<TDbContext>();
    }
}
