using System.Data;
using System.Runtime.CompilerServices;
using BoardOil.Abstractions.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Scope;

public class DbContextScope : IDbContextScope
{
    private static readonly ConditionalWeakTable<InstanceIdentifier, DbContextScope> ScopeInstances = new();
    private static readonly AsyncLocal<InstanceIdentifier?> AmbientIdentifier = new();

    private readonly InstanceIdentifier _instanceIdentifier = new();
    private readonly bool _readOnly;

    private bool _disposed;
    private bool _completed;
    private bool _nested;
    private DbContextScope? _parentScope;
    private DbContextCollection _dbContexts;

    public IDbContextCollection DbContexts => _dbContexts;

    public DbContextScope(
        DbContextScopeOption joiningOption,
        bool readOnly,
        IsolationLevel? isolationLevel,
        IDbContextFactory dbContextFactory)
    {
        if (isolationLevel.HasValue && joiningOption == DbContextScopeOption.JoinExisting)
        {
            throw new ArgumentException(
                "Cannot join an ambient scope when an explicit database transaction is required.");
        }

        _readOnly = readOnly;
        _parentScope = GetAmbientScope();

        if (_parentScope != null && joiningOption == DbContextScopeOption.JoinExisting)
        {
            if (_parentScope._readOnly && !_readOnly)
            {
                throw new InvalidOperationException(
                    "Cannot nest a read/write scope inside a read-only scope.");
            }

            _nested = true;
            _dbContexts = _parentScope._dbContexts;
        }
        else
        {
            _nested = false;
            _dbContexts = new DbContextCollection(_readOnly, isolationLevel, dbContextFactory);
        }

        SetAmbientScope(this);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DbContextScope));
        }

        if (_completed)
        {
            throw new InvalidOperationException("SaveChangesAsync can only be called once per scope.");
        }

        return SaveInternalAsync(cancellationToken);
    }

    public async Task Transaction(Func<IDbContextTransactionScope, IDbContextTransaction, Task> executor)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DbContextScope));
        }

        var dbContext = DbContexts.Get<BoardOilDbContext>();

        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync().ConfigureAwait(false);
            var transactionScope = new DbContextTransactionScope(dbContext);
            var transactionWrapper = new DbContextTransaction(transaction);
            await executor(transactionScope, transactionWrapper).ConfigureAwait(false);
        }).ConfigureAwait(false);

        _completed = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (!_nested)
        {
            if (!_completed)
            {
                try
                {
                    if (_readOnly)
                    {
                        CommitInternal();
                    }
                    else
                    {
                        RollbackInternal();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }

                _completed = true;
            }

            _dbContexts.Dispose();
        }

        var currentAmbientScope = GetAmbientScope();
        if (currentAmbientScope != this)
        {
            throw new InvalidOperationException(
                "DbContextScope instances must be disposed in creation order.");
        }

        RemoveAmbientScope();

        if (_parentScope != null)
        {
            if (_parentScope._disposed)
            {
                var message =
                    "Parent scope was already disposed. Suppress ambient context before starting parallel work.";
                System.Diagnostics.Debug.WriteLine(message);
            }
            else
            {
                SetAmbientScope(_parentScope);
            }
        }

        _disposed = true;
    }

    internal static DbContextScope? GetAmbientScope()
    {
        var identifier = AmbientIdentifier.Value;
        if (identifier == null)
        {
            return null;
        }

        if (ScopeInstances.TryGetValue(identifier, out var scope))
        {
            return scope;
        }

        System.Diagnostics.Debug.WriteLine(
            "Found ambient scope identifier but no corresponding scope instance.");
        return null;
    }

    internal static void SetAmbientScope(DbContextScope newAmbientScope)
    {
        if (newAmbientScope == null)
        {
            throw new ArgumentNullException(nameof(newAmbientScope));
        }

        if (AmbientIdentifier.Value == newAmbientScope._instanceIdentifier)
        {
            return;
        }

        AmbientIdentifier.Value = newAmbientScope._instanceIdentifier;
        ScopeInstances.GetValue(newAmbientScope._instanceIdentifier, _ => newAmbientScope);
    }

    internal static void RemoveAmbientScope()
    {
        var current = AmbientIdentifier.Value;
        AmbientIdentifier.Value = null;

        if (current != null)
        {
            ScopeInstances.Remove(current);
        }
    }

    internal static void HideAmbientScope()
    {
        AmbientIdentifier.Value = null;
    }

    private async Task<int> SaveInternalAsync(CancellationToken cancellationToken)
    {
        var affectedRows = 0;

        if (!_nested)
        {
            affectedRows = await CommitInternalAsync(cancellationToken).ConfigureAwait(false);
        }

        _completed = true;
        return affectedRows;
    }

    private int CommitInternal()
    {
        try
        {
            return _dbContexts.Commit();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException(ex.Message, ex);
        }
    }

    private async Task<int> CommitInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _dbContexts.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException(ex.Message, ex);
        }
    }

    private void RollbackInternal()
    {
        _dbContexts.Rollback();
    }

    private sealed class InstanceIdentifier : MarshalByRefObject
    {
    }
}

public sealed class DbContextTransactionScope(DbContext dbContext) : IDbContextTransactionScope
{
    private readonly DbContext _dbContext = dbContext;

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DbContextTransaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    : IDbContextTransaction
{
    private readonly Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _transaction = transaction;

    public Task CommitAsync()
    {
        return _transaction.CommitAsync();
    }

    public void Dispose()
    {
        _transaction.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _transaction.DisposeAsync();
    }
}
