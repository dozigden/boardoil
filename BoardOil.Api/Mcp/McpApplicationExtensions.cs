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
        var mcpOptions = app.Services.GetRequiredService<BoardOilMcpOptions>();

        app.Use(async (context, next) =>
        {
            if (mcpOptions.AuthMode is McpAuthMode.Pat
                && IsMcpAuthRequiredPath(context.Request.Path, mcpOptions))
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
            if (IsUnsupportedMcpStylePath(context.Request.Path, mcpOptions))
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

        var mcpEndpoint = app.MapMcp("/mcp");
        if (mcpOptions.AuthMode is McpAuthMode.Pat)
        {
            mcpEndpoint.RequireAuthorization(BoardOilPolicies.McpAuthenticated);
        }

        app.MapGet("/.well-known/mcp", async (IConfigurationService configurationService) =>
            Results.Json(McpDiscoveryMetadata.CreateWellKnownDocument(
                await configurationService.GetMcpPublicBaseUrlAsync(),
                mcpOptions)));

        return app;
    }

    private static bool IsUnsupportedMcpStylePath(PathString path, BoardOilMcpOptions mcpOptions) =>
        (!mcpOptions.SupportsLegacySseTransport
            && path.StartsWithSegments("/sse", StringComparison.OrdinalIgnoreCase))
        || (!mcpOptions.SupportsLegacySseTransport
            && path.StartsWithSegments("/messages", StringComparison.OrdinalIgnoreCase))
        || path.StartsWithSegments("/v1/mcp", StringComparison.OrdinalIgnoreCase);

    private static bool IsMcpAuthRequiredPath(PathString path, BoardOilMcpOptions mcpOptions) =>
        path.StartsWithSegments("/mcp", StringComparison.OrdinalIgnoreCase)
        || (mcpOptions.SupportsLegacySseTransport
            && path.StartsWithSegments("/sse", StringComparison.OrdinalIgnoreCase))
        || (mcpOptions.SupportsLegacySseTransport
            && path.StartsWithSegments("/messages", StringComparison.OrdinalIgnoreCase));
}
