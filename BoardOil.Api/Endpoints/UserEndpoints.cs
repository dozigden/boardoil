using BoardOil.Api.Extensions;
using BoardOil.Abstractions.Users;
using BoardOil.Contracts.Users;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/users", (IUserService userService) =>
                userService.GetUsersAsync().ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser);

        app.MapGet("/api/admin/users", (IUserAdminService userAdminService) =>
                userAdminService.GetUsersAsync().ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);
        app.MapPost("/api/admin/users", (CreateUserRequest request, IUserAdminService userAdminService) =>
                userAdminService.CreateUserAsync(request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);
        app.MapPut("/api/admin/users/{id:int}/role", (int id, UpdateUserRoleRequest request, IUserAdminService userAdminService) =>
                userAdminService.UpdateUserRoleAsync(id, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);
        app.MapPut("/api/admin/users/{id:int}/status", (int id, UpdateUserStatusRequest request, IUserAdminService userAdminService) =>
                userAdminService.UpdateUserStatusAsync(id, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);

        return app;
    }
}
