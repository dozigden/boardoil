using BoardOil.Abstractions.OAuth;
using BoardOil.Api.Extensions;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class OAuthTokenAuditEndpoints
{
    public static IEndpointRouteBuilder MapOAuthTokenAuditEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/system/oauth-token-audits", async (
                int? offset,
                int? limit,
                DateTime? fromUtc,
                DateTime? toUtc,
                string? outcome,
                string? grantType,
                int? connectionId,
                string? clientId,
                string? authorizationId,
                string? tokenFingerprint,
                IOAuthTokenAuditService auditService) =>
                (await auditService.ListAsync(
                    offset,
                    limit,
                    fromUtc,
                    toUtc,
                    outcome,
                    grantType,
                    connectionId,
                    clientId,
                    authorizationId,
                    tokenFingerprint)).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System OAuth Connections");

        app.MapPost(
                "/api/system/oauth-token-audits:purge",
                async (IOAuthTokenAuditService auditService, CancellationToken cancellationToken) =>
                    (await auditService.PurgeExpiredAsync(cancellationToken)).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System OAuth Connections");

        return app;
    }
}
