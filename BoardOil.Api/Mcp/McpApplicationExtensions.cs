using BoardOil.Api.Configuration;
using BoardOil.Api.OAuth;
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
            if (OAuthResources.IsMcpPath(context.Request.Path))
            {
                context.Response.OnStarting(async () =>
                {
                    McpOAuthChallengeState.TryGet(context, out var challenge);
                    if (challenge?.Error is McpOAuthChallengeError.InvalidToken)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    }
                    else if (challenge?.Error is McpOAuthChallengeError.InsufficientScope)
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    }

                    if (context.Response.StatusCode is not (
                            StatusCodes.Status401Unauthorized
                            or StatusCodes.Status403Forbidden))
                    {
                        return;
                    }

                    var metadataService = context.RequestServices
                        .GetRequiredService<IOAuthProtectedResourceMetadataService>();
                    var resourcePath = OAuthResources.ResolveResourcePath(context.Request.Path);
                    var metadata = await metadataService.GetMcpAsync(context.Request, resourcePath);
                    var urlResolver = context.RequestServices
                        .GetRequiredService<OAuthEndpointUrlResolver>();
                    var metadataUrl = await urlResolver.ResolveAsync(
                        context.Request,
                        OAuthResources.ResolveMetadataPath(context.Request.Path));
                    var parameters = new List<string>();
                    var error = challenge?.Error;
                    if (error is null
                        && context.Response.StatusCode == StatusCodes.Status401Unauthorized
                        && HasBearerAuthenticationAttempt(context.Request))
                    {
                        error = McpOAuthChallengeError.InvalidToken;
                    }

                    if (error is McpOAuthChallengeError.InvalidToken)
                    {
                        parameters.Add("error=\"invalid_token\"");
                    }
                    else if (error is McpOAuthChallengeError.InsufficientScope)
                    {
                        parameters.Add("error=\"insufficient_scope\"");
                    }

                    parameters.Add($"resource_metadata=\"{metadataUrl}\"");
                    var challengedScopes = challenge?.RequiredScope;
                    if (string.IsNullOrWhiteSpace(challengedScopes))
                    {
                        challengedScopes = string.Join(' ', metadata.ScopesSupported);
                    }

                    if (!string.IsNullOrWhiteSpace(challengedScopes))
                    {
                        parameters.Add($"scope=\"{challengedScopes}\"");
                    }

                    context.Response.Headers.WWWAuthenticate = $"Bearer {string.Join(", ", parameters)}";
                });
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
            mcpEndpoint.RequireAuthorization(BoardOilPolicies.McpCanonicalConnection);
        }

        app.MapMcp(OAuthResources.LegacyMcpPath)
            .RequireAuthorization(BoardOilPolicies.McpOAuthConnection);

        if (!mcpOptions.SupportsLegacySseTransport)
        {
            var canonicalGetEndpoint = app.MapGet(
                OAuthResources.McpPath,
                CreateStatelessGetResponse);
            if (mcpOptions.AuthMode is McpAuthMode.Pat)
            {
                canonicalGetEndpoint.RequireAuthorization(BoardOilPolicies.McpCanonicalConnection);
            }

            app.MapGet(
                    OAuthResources.LegacyMcpPath,
                    CreateStatelessGetResponse)
                .RequireAuthorization(BoardOilPolicies.McpOAuthConnection);
        }

        app.MapGet("/.well-known/mcp", async (IConfigurationService configurationService) =>
            Results.Json(McpDiscoveryMetadata.CreateWellKnownDocument(
                await configurationService.GetMcpPublicBaseUrlAsync(),
                mcpOptions)));

        return app;
    }

    private static IResult CreateStatelessGetResponse(HttpResponse response)
    {
        response.Headers.Allow = HttpMethods.Post;
        return Results.StatusCode(StatusCodes.Status405MethodNotAllowed);
    }

    public static WebApplication LogMcpStartupWarnings(this WebApplication app)
    {
        var mcpOptions = app.Services.GetRequiredService<BoardOilMcpOptions>();
        if (mcpOptions.AuthMode is McpAuthMode.None)
        {
            app.Logger.LogWarning(
                "MCP auth mode is configured as 'none'. MCP endpoints are unauthenticated and should only be exposed in trusted environments.");
        }

        return app;
    }

    private static bool IsUnsupportedMcpStylePath(PathString path, BoardOilMcpOptions mcpOptions) =>
        (!mcpOptions.SupportsLegacySseTransport && IsLegacySsePath(path))
        || path.StartsWithSegments("/sse", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/message", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/messages", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/v1/mcp", StringComparison.OrdinalIgnoreCase);

    private static bool IsLegacySsePath(PathString path) =>
        path.StartsWithSegments("/mcp/sse", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/mcp/message", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/mcp/oauth/sse", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/mcp/oauth/message", StringComparison.OrdinalIgnoreCase);

    private static bool HasBearerAuthenticationAttempt(HttpRequest request)
    {
        var authorization = request.Headers.Authorization.ToString();
        return string.Equals(authorization, "Bearer", StringComparison.OrdinalIgnoreCase)
            || authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
    }
}
