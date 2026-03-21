using Microsoft.EntityFrameworkCore;

namespace BoardOil.Abstractions.DataAccess;

public interface IDbContextCollection : IDisposable
{
    TDbContext Get<TDbContext>() where TDbContext : DbContext;
    int Commit();
    Task<int> CommitAsync(CancellationToken cancellationToken = default);
    void Rollback();
}
