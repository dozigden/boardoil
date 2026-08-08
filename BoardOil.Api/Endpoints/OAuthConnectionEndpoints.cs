using System.Security.Claims;
using BoardOil.Abstractions.OAuth;
using BoardOil.Api.Auth;
using BoardOil.Api.Extensions;
using BoardOil.Contracts.Common;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class OAuthConnectionEndpoints
{
    public static IEndpointRouteBuilder MapOAuthConnectionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/oauth-connections", async (
                ClaimsPrincipal user,
                IOAuthConnectionManagementService connectionService) =>
            {
                if (!user.TryGetUserId(out var actorUserId))
                {
                    return ApiErrors.Unauthorized("Invalid identity context.").ToHttpResult();
                }

                return (await connectionService.GetOwnConnectionsAsync(actorUserId)).ToHttpResult();
            })
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .WithTags("OAuth Connections");

        app.MapDelete("/api/oauth-connections/{id:int}", async (
                int id,
                ClaimsPrincipal user,
                IOAuthConnectionManagementService connectionService,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetUserId(out var actorUserId))
                {
                    return ApiErrors.Unauthorized("Invalid identity context.").ToHttpResult();
                }

                return (await connectionService.RevokeOwnConnectionAsync(id, actorUserId, cancellationToken)).ToHttpResult();
            })
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .WithTags("OAuth Connections");

        app.MapGet("/api/system/oauth-connections", (
                IOAuthConnectionManagementService connectionService) =>
                connectionService.GetAllConnectionsAsync().ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System OAuth Connections");

        app.MapDelete("/api/system/oauth-connections/{id:int}", async (
                int id,
                ClaimsPrincipal user,
                IOAuthConnectionManagementService connectionService,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetUserId(out var actorUserId))
                {
                    return ApiErrors.Unauthorized("Invalid identity context.").ToHttpResult();
                }

                return (await connectionService.RevokeConnectionAsAdminAsync(id, actorUserId, cancellationToken)).ToHttpResult();
            })
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System OAuth Connections");

        return app;
    }
}
