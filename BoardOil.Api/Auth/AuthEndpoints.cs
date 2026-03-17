using BoardOil.Api.Extensions;
using BoardOil.Services.Auth;
using BoardOil.Services.Users;
using System.Security.Claims;

namespace BoardOil.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/register-initial-admin", (RegisterInitialAdminRequest request, IAuthHttpSessionService authHttpService, HttpResponse response) =>
                authHttpService.RegisterInitialAdminAsync(request, response));
        app.MapPost("/api/auth/login", (LoginRequest request, IAuthHttpSessionService authHttpService, HttpResponse response) =>
                authHttpService.LoginAsync(request, response));
        app.MapPost("/api/auth/refresh", (IAuthHttpSessionService authHttpService, HttpRequest request, HttpResponse response) =>
                authHttpService.RefreshAsync(request, response));
        app.MapPost("/api/auth/logout", (IAuthHttpSessionService authHttpService, HttpRequest request, HttpResponse response) =>
                authHttpService.LogoutAsync(request, response));

        app.MapGet("/api/auth/csrf", (IAuthHttpSessionService authHttpService, HttpRequest request, HttpResponse response) =>
                authHttpService.GetCsrf(request, response))
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser);
        app.MapGet("/api/auth/me", (IAuthHttpSessionService authHttpService, ClaimsPrincipal claimsPrincipal) =>
                authHttpService.GetMeAsync(claimsPrincipal))
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser);

        app.MapGet("/api/users", (IUserAdminService userAdminService) =>
                userAdminService.GetUsersAsync().ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);
        app.MapPost("/api/users", (CreateUserRequest request, IUserAdminService userAdminService) =>
                userAdminService.CreateUserAsync(request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);
        app.MapPatch("/api/users/{id:int}/role", (int id, UpdateUserRoleRequest request, IUserAdminService userAdminService) =>
                userAdminService.UpdateUserRoleAsync(id, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);
        app.MapPatch("/api/users/{id:int}/status", (int id, UpdateUserStatusRequest request, IUserAdminService userAdminService) =>
                userAdminService.UpdateUserStatusAsync(id, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);

        return app;
    }
}
