using System.Security.Claims;
using BoardOil.Api.Auth;
using BoardOil.Api.Extensions;
using BoardOil.Abstractions.Users;
using BoardOil.Contracts.Contracts;
using BoardOil.Contracts.Users;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/users", (IUserService userService) =>
                userService.GetUsersAsync().ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .WithTags("Users");

        app.MapGet("/api/system/users", (IUserAdminService userAdminService) =>
                userAdminService.GetUsersAsync().ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System Users");
        app.MapPost("/api/system/users", (CreateUserRequest request, IUserAdminService userAdminService) =>
                userAdminService.CreateUserAsync(request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System Users");
        app.MapPut("/api/system/users/{id:int}/role", (int id, UpdateUserRoleRequest request, IUserAdminService userAdminService) =>
                userAdminService.UpdateUserRoleAsync(id, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System Users");
        app.MapPut("/api/system/users/{id:int}/status", (int id, UpdateUserStatusRequest request, IUserAdminService userAdminService) =>
                userAdminService.UpdateUserStatusAsync(id, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System Users");
        app.MapDelete("/api/system/users/{id:int}", async (int id, ClaimsPrincipal user, IUserAdminService userAdminService) =>
            {
                if (!user.TryGetUserId(out var actorUserId))
                {
                    return ((ApiResult)ApiErrors.Unauthorized("Invalid identity context.")).ToHttpResult();
                }

                return (await userAdminService.DeleteUserAsync(id, actorUserId)).ToHttpResult();
            })
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System Users");

        return app;
    }
}
