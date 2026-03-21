namespace BoardOil.Abstractions.DataAccess;

public interface IDbContextScope : IDisposable
{
    IDbContextCollection DbContexts { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task Transaction(Func<IDbContextTransactionScope, IDbContextTransaction, Task> executor);
}
