using System.Data;
using BoardOil.Abstractions.DataAccess;

namespace BoardOil.Ef.Scope;

public sealed class DbContextReadOnlyScope(
    DbContextScopeOption joiningOption,
    IsolationLevel? isolationLevel,
    IDbContextFactory dbContextFactory)
    : DbContextScope(joiningOption, readOnly: true, isolationLevel, dbContextFactory), IDbContextReadOnlyScope
{
}
