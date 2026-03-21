namespace BoardOil.Abstractions.DataAccess;

public interface IDbContextTransaction : IDisposable, IAsyncDisposable
{
    Task CommitAsync();
}

public interface IDbContextTransactionScope
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
