namespace BoardOil.Ef.Scope;

public sealed class AmbientContextSuppressor : IDisposable
{
    private readonly DbContextScope? _savedScope;
    private bool _disposed;

    public AmbientContextSuppressor()
    {
        _savedScope = DbContextScope.GetAmbientScope();
        DbContextScope.HideAmbientScope();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_savedScope != null)
        {
            DbContextScope.SetAmbientScope(_savedScope);
        }

        _disposed = true;
    }
}
