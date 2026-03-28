using BoardOil.Api.Extensions;
using BoardOil.Api.Configuration;
using BoardOil.Abstractions.Auth;
using BoardOil.Abstractions.Users;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Contracts;
using BoardOil.Contracts.Users;
using BoardOil.Services.Auth;
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
        app.MapPost("/api/auth/machine/login", (LoginRequest request, IAuthService authService) =>
                LoginMachineAsync(request, authService));
        app.MapPost("/api/auth/machine/refresh", (MachineRefreshRequest request, IAuthService authService) =>
                RefreshMachineAsync(request, authService));
        app.MapPost("/api/auth/machine/logout", (MachineLogoutRequest request, IAuthService authService) =>
                LogoutMachineAsync(request, authService));

        app.MapGet("/api/auth/csrf", (IAuthHttpSessionService authHttpService, HttpRequest request, HttpResponse response) =>
                authHttpService.GetCsrf(request, response))
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser);
        app.MapGet("/api/configuration", (IConfigurationService configurationService) =>
                ApiResults.Ok(configurationService.GetConfiguration()).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);
        app.MapGet("/api/auth/me", (IAuthHttpSessionService authHttpService, ClaimsPrincipal claimsPrincipal) =>
                authHttpService.GetMeAsync(claimsPrincipal))
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser);
        app.MapGet("/api/auth/bootstrap-status", (IAuthHttpSessionService authHttpService) =>
                authHttpService.GetBootstrapStatusAsync());

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

    private static async Task<IResult> LoginMachineAsync(LoginRequest request, IAuthService authService)
    {
        var result = await authService.LoginAsync(request);
        if (!result.Success || result.Data is null)
        {
            return result.ToHttpResult();
        }

        return ApiResults.Ok(result.Data.ToMachineDto()).ToHttpResult();
    }

    private static async Task<IResult> RefreshMachineAsync(MachineRefreshRequest request, IAuthService authService)
    {
        var result = await authService.RefreshAsync(request.RefreshToken);
        if (!result.Success || result.Data is null)
        {
            return result.ToHttpResult();
        }

        return ApiResults.Ok(result.Data.ToMachineDto()).ToHttpResult();
    }

    private static async Task<IResult> LogoutMachineAsync(MachineLogoutRequest request, IAuthService authService)
    {
        var result = await authService.LogoutAsync(request.RefreshToken);
        return result.ToHttpResult();
    }
}
