using BoardOil.Data.Abstractions.Users;
using Microsoft.Data.Sqlite;

namespace BoardOil.Ef.Functions;

internal static class SqliteEmailCollation
{
    internal const string Name = "BO_EMAIL";

    internal static void Register(SqliteConnection connection)
    {
        connection.CreateCollation(Name, EmailComparison.Compare);
        connection.CreateFunction<string?, string?>("bo_normalise_email", EmailComparison.Normalise, isDeterministic: true);
    }
}
