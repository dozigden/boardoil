using System.Security.Claims;
using BoardOil.Abstractions.Mcp;
using BoardOil.Api.Auth;
using BoardOil.Api.Extensions;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.Mcp;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class McpProjectConnectionEndpoints
{
    public static IEndpointRouteBuilder MapMcpProjectConnectionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/system/mcp-project-connections", (IMcpProjectConnectionService service) =>
                service.GetConnectionsAsync().ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System MCP Project Connections");

        app.MapPost("/api/system/mcp-project-connections", async (
                CreateMcpProjectConnectionRequest request,
                ClaimsPrincipal user,
                IMcpProjectConnectionService service) =>
            {
                if (!user.TryGetUserId(out var actorUserId))
                {
                    return ApiErrors.Unauthorized("Invalid identity context.").ToHttpResult();
                }

                return (await service.CreateConnectionAsync(actorUserId, request)).ToHttpResult();
            })
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System MCP Project Connections");

        app.MapDelete("/api/system/mcp-project-connections/{id:int}", async (
                int id,
                ClaimsPrincipal user,
                IMcpProjectConnectionService service) =>
            {
                if (!user.TryGetUserId(out var actorUserId))
                {
                    return ApiErrors.Unauthorized("Invalid identity context.").ToHttpResult();
                }

                return (await service.RevokeConnectionAsync(actorUserId, id)).ToHttpResult();
            })
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System MCP Project Connections");

        return app;
    }
}
