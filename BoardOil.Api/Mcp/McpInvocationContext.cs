namespace BoardOil.Api.Mcp;

public sealed record McpInvocationContext(
    IServiceProvider Services,
    int ActorUserId,
    McpAccessContext? AccessContext,
    string CorrelationId);
