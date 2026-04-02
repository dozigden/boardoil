using BoardOil.Api.Configuration;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Mcp;

public static class McpApplicationExtensions
{
    public static WebApplication InitialiseMcpServiceProvider(this WebApplication app)
    {
        _ = app.Services.GetRequiredService<McpToolRegistry>();
        app.Services.GetRequiredService<McpServiceProviderAccessor>().Initialise(app.Services);
        return app;
    }

    public static WebApplication MapBoardOilMcp(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/mcp", StringComparison.OrdinalIgnoreCase))
            {
                var authHeader = context.Request.Headers.Authorization.ToString();
                if (string.IsNullOrWhiteSpace(authHeader)
                    || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    var configurationService = context.RequestServices.GetRequiredService<IConfigurationService>();
                    var errorFactory = context.RequestServices.GetRequiredService<IMcpErrorResponseFactory>();
                    var mcpPublicBaseUrl = await configurationService.GetMcpPublicBaseUrlAsync();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.Headers.WWWAuthenticate = "Bearer realm=\"BoardOil MCP\"";
                    await context.Response.WriteAsJsonAsync(errorFactory.CreateAuthError(mcpPublicBaseUrl, "Missing bearer token."));
                    return;
                }
            }

            await next();
        });

        app.Use(async (context, next) =>
        {
            if (IsUnsupportedMcpStylePath(context.Request.Path))
            {
                var configurationService = context.RequestServices.GetRequiredService<IConfigurationService>();
                var errorFactory = context.RequestServices.GetRequiredService<IMcpErrorResponseFactory>();
                var mcpPublicBaseUrl = await configurationService.GetMcpPublicBaseUrlAsync();
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(errorFactory.CreateUnsupportedMcpPathError(context.Request.Path, mcpPublicBaseUrl));
                return;
            }

            await next();
        });

        app.MapMcp("/mcp")
            .RequireAuthorization(BoardOilPolicies.McpAuthenticated);

        app.MapGet("/.well-known/mcp", async (IConfigurationService configurationService) =>
            Results.Json(McpDiscoveryMetadata.CreateWellKnownDocument(await configurationService.GetMcpPublicBaseUrlAsync())));

        return app;
    }

    private static bool IsUnsupportedMcpStylePath(PathString path) =>
        path.StartsWithSegments("/sse", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/messages", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/v1/mcp", StringComparison.OrdinalIgnoreCase);
}
