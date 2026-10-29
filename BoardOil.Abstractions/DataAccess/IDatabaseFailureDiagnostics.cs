namespace BoardOil.Abstractions.DataAccess;

public interface IDatabaseFailureDiagnostics
{
    void Enrich(Exception exception);
}
