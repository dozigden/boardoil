namespace BoardOil.Mcp.Server.Mcp;

public sealed class McpServiceProviderAccessor
{
    private IServiceProvider? _serviceProvider;

    public IServiceProvider ServiceProvider =>
        _serviceProvider ?? throw new InvalidOperationException("MCP service provider is not initialised.");

    public void Initialise(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
}
