namespace BoardOil.Api.Mcp;

public sealed record McpInvocationContext(
    IServiceProvider Services,
    int ActorUserId,
    PatAccessContext? PatAccessContext,
    string CorrelationId);
