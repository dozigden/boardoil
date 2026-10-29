namespace BoardOil.Abstractions.DataAccess;

public sealed class DatabaseDiagnosticOperation : IDisposable
{
    private static readonly AsyncLocal<string?> CurrentName = new();
    private readonly string? previous = CurrentName.Value;

    private DatabaseDiagnosticOperation(string name) => CurrentName.Value = name;

    public static string? Current => CurrentName.Value;

    public static DatabaseDiagnosticOperation Begin(string name) => new(name);

    public void Dispose() => CurrentName.Value = previous;
}
