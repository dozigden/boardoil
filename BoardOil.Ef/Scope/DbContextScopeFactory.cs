using System.Data;
using BoardOil.Abstractions.DataAccess;

namespace BoardOil.Ef.Scope;

public sealed class DbContextScopeFactory(IDbContextFactory dbContextFactory) : IDbContextScopeFactory
{
    private readonly IDbContextFactory _dbContextFactory = dbContextFactory;

    public IDbContextScope Create(DbContextScopeOption joiningOption = DbContextScopeOption.JoinExisting) =>
        new DbContextScope(joiningOption, readOnly: false, isolationLevel: null, _dbContextFactory);

    public IDbContextReadOnlyScope CreateReadOnly(DbContextScopeOption joiningOption = DbContextScopeOption.JoinExisting) =>
        new DbContextReadOnlyScope(joiningOption, isolationLevel: null, _dbContextFactory);

    public IDbContextScope CreateWithTransaction(IsolationLevel isolationLevel) =>
        new DbContextScope(DbContextScopeOption.ForceCreateNew, readOnly: false, isolationLevel, _dbContextFactory);

    public IDbContextReadOnlyScope CreateReadOnlyWithTransaction(IsolationLevel isolationLevel) =>
        new DbContextReadOnlyScope(DbContextScopeOption.ForceCreateNew, isolationLevel, _dbContextFactory);

    public IDisposable SuppressAmbientContext() =>
        new AmbientContextSuppressor();
}
