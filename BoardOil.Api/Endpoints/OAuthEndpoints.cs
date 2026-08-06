using BoardOil.Api.OAuth;

namespace BoardOil.Api.Endpoints;

public static class OAuthEndpoints
{
    public static IEndpointRouteBuilder MapOAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/connect/register", async (
                OAuthDynamicClientRegistrationRequest request,
                IOAuthDynamicClientRegistrationService registrationService,
                CancellationToken cancellationToken) =>
            {
                var result = await registrationService.RegisterAsync(request, cancellationToken);
                if (!result.Success || result.Registration is null)
                {
                    return Results.Json(result.Error, statusCode: StatusCodes.Status400BadRequest);
                }

                return Results.Json(result.Registration, statusCode: StatusCodes.Status201Created);
            })
            .RequireRateLimiting(OAuthServiceCollectionExtensions.DynamicClientRegistrationRateLimitPolicy)
            .WithTags("OAuth");

        app.MapGet("/.well-known/oauth-protected-resource/mcp/connections/{publicId}", async (
                string publicId,
                HttpRequest request,
                IOAuthProtectedResourceMetadataService metadataService) =>
            {
                var metadata = await metadataService.GetAsync(publicId, request);
                return metadata is null
                    ? Results.NotFound()
                    : Results.Json(metadata);
            })
            .WithTags("OAuth");

        app.MapGet("/mcp/connections/{publicId}", async (
                string publicId,
                HttpContext context,
                IOAuthProtectedResourceMetadataService metadataService,
                OAuthEndpointUrlResolver urlResolver) =>
            {
                var metadata = await metadataService.GetAsync(publicId, context.Request);
                if (metadata is null)
                {
                    return Results.NotFound();
                }

                var metadataUrl = await urlResolver.ResolveAsync(
                    context.Request,
                    $"/.well-known/oauth-protected-resource/mcp/connections/{publicId}");
                var scopes = string.Join(' ', metadata.ScopesSupported);
                context.Response.Headers.WWWAuthenticate =
                    $"Bearer resource_metadata=\"{metadataUrl}\", scope=\"{scopes}\"";
                return Results.StatusCode(StatusCodes.Status401Unauthorized);
            })
            .WithTags("OAuth");

        return app;
    }
}
