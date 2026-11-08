using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Functions;

public static class SqliteStringFunctions
{
    private const string UpperInvariantFunctionName = "bo_upper_invariant";

    public static string? UpperInvariant(string? value) => value?.ToUpperInvariant();

    internal static void ConfigureModel(ModelBuilder modelBuilder) =>
        modelBuilder.HasDbFunction(typeof(SqliteStringFunctions).GetMethod(nameof(UpperInvariant))!)
            .HasName(UpperInvariantFunctionName)
            .HasParameter("value").PropagatesNullability();

    internal static void Register(SqliteConnection connection) =>
        connection.CreateFunction<string?, string?>(
            UpperInvariantFunctionName, UpperInvariant, isDeterministic: true);
}
