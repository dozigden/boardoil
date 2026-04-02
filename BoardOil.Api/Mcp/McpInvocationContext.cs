namespace BoardOil.Api.Mcp;

public sealed record McpInvocationContext(
    IServiceProvider Services,
    PatAccessContext? PatAccessContext,
    string CorrelationId);
