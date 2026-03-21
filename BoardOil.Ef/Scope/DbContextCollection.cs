using System.Data;
using System.Runtime.ExceptionServices;
using BoardOil.Abstractions.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Scope;

public sealed class DbContextCollection : IDbContextCollection
{
    private readonly Dictionary<Type, DbContext> _initializedDbContexts = new();
    private readonly Dictionary<DbContext, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> _transactions = new();
    private readonly IsolationLevel? _isolationLevel;
    private readonly IDbContextFactory _dbContextFactory;
    private readonly bool _readOnly;

    private bool _disposed;
    private bool _completed;

    public DbContextCollection(bool readOnly, IsolationLevel? isolationLevel, IDbContextFactory dbContextFactory)
    {
        _readOnly = readOnly;
        _isolationLevel = isolationLevel;
        _dbContextFactory = dbContextFactory;
    }

    public TDbContext Get<TDbContext>() where TDbContext : DbContext
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DbContextCollection));
        }

        var requestedType = typeof(TDbContext);

        if (_initializedDbContexts.TryGetValue(requestedType, out var existingContext))
        {
            return (TDbContext)existingContext;
        }

        var newContext = _dbContextFactory.CreateDbContext<TDbContext>();
        _initializedDbContexts.Add(requestedType, newContext);

        if (_readOnly)
        {
            newContext.ChangeTracker.AutoDetectChangesEnabled = false;
        }

        if (_isolationLevel.HasValue)
        {
            var transaction = newContext.Database.BeginTransaction(_isolationLevel.Value);
            _transactions.Add(newContext, transaction);
        }

        return newContext;
    }

    public int Commit()
    {
        EnsureCanComplete();

        ExceptionDispatchInfo? lastError = null;
        var affectedRows = 0;

        foreach (var dbContext in _initializedDbContexts.Values)
        {
            try
            {
                if (!_readOnly)
                {
                    affectedRows += dbContext.SaveChanges();
                }

                CommitTransactionIfPresent(dbContext);
            }
            catch (Exception ex)
            {
                lastError = ExceptionDispatchInfo.Capture(ex);
            }
        }

        _transactions.Clear();
        _completed = true;

        lastError?.Throw();
        return affectedRows;
    }

    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        EnsureCanComplete();

        ExceptionDispatchInfo? lastError = null;
        var affectedRows = 0;

        foreach (var dbContext in _initializedDbContexts.Values)
        {
            try
            {
                if (!_readOnly)
                {
                    for (var attempt = 0; attempt < 2; attempt++)
                    {
                        try
                        {
                            affectedRows += await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                            break;
                        }
                        catch (DbUpdateConcurrencyException ex) when (attempt == 0)
                        {
                            var canRetry = false;

                            foreach (var entry in ex.Entries)
                            {
                                if (entry.State == EntityState.Deleted)
                                {
                                    canRetry = true;
                                    await entry.ReloadAsync(cancellationToken).ConfigureAwait(false);
                                }
                            }

                            if (!canRetry)
                            {
                                throw;
                            }
                        }
                    }
                }

                CommitTransactionIfPresent(dbContext);
            }
            catch (Exception ex)
            {
                lastError = ExceptionDispatchInfo.Capture(ex);
            }
        }

        _transactions.Clear();
        _completed = true;

        lastError?.Throw();
        return affectedRows;
    }

    public void Rollback()
    {
        EnsureCanComplete();

        ExceptionDispatchInfo? lastError = null;

        foreach (var dbContext in _initializedDbContexts.Values)
        {
            if (!_transactions.TryGetValue(dbContext, out var transaction))
            {
                continue;
            }

            try
            {
                transaction.Rollback();
                transaction.Dispose();
            }
            catch (Exception ex)
            {
                lastError = ExceptionDispatchInfo.Capture(ex);
            }
        }

        _transactions.Clear();
        _completed = true;

        lastError?.Throw();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (!_completed)
        {
            try
            {
                if (_readOnly)
                {
                    Commit();
                }
                else
                {
                    Rollback();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        foreach (var dbContext in _initializedDbContexts.Values)
        {
            try
            {
                dbContext.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        _initializedDbContexts.Clear();
        _disposed = true;
    }

    private void EnsureCanComplete()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DbContextCollection));
        }

        if (_completed)
        {
            throw new InvalidOperationException("Commit/Rollback can only be called once per collection.");
        }
    }

    private void CommitTransactionIfPresent(DbContext dbContext)
    {
        if (_transactions.TryGetValue(dbContext, out var transaction))
        {
            transaction.Commit();
            transaction.Dispose();
        }
    }
}
