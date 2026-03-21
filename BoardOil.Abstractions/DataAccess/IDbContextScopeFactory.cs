using System.Data;

namespace BoardOil.Abstractions.DataAccess;

public interface IDbContextScopeFactory
{
    IDbContextScope Create(DbContextScopeOption joiningOption = DbContextScopeOption.JoinExisting);
    IDbContextReadOnlyScope CreateReadOnly(DbContextScopeOption joiningOption = DbContextScopeOption.JoinExisting);
    IDbContextScope CreateWithTransaction(IsolationLevel isolationLevel);
    IDbContextReadOnlyScope CreateReadOnlyWithTransaction(IsolationLevel isolationLevel);
    IDisposable SuppressAmbientContext();
}
