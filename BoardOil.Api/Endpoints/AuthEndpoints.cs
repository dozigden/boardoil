using BoardOil.Api.Extensions;
using BoardOil.Abstractions.Auth;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Contracts;
using BoardOil.Services.Auth;
using System.Security.Claims;
using BoardOil.Api.Auth;

namespace BoardOil.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/register-initial-admin", (RegisterInitialAdminRequest request, IAuthHttpSessionService authHttpService, HttpResponse response) =>
                authHttpService.RegisterInitialAdminAsync(request, response))
            .WithTags("Auth");
        app.MapPost("/api/auth/login", (LoginRequest request, IAuthHttpSessionService authHttpService, HttpResponse response) =>
                authHttpService.LoginAsync(request, response))
            .WithTags("Auth");
        app.MapPost("/api/auth/refresh", (IAuthHttpSessionService authHttpService, HttpRequest request, HttpResponse response) =>
                authHttpService.RefreshAsync(request, response))
            .WithTags("Auth");
        app.MapPost("/api/auth/logout", (IAuthHttpSessionService authHttpService, HttpRequest request, HttpResponse response) =>
                authHttpService.LogoutAsync(request, response))
            .WithTags("Auth");
        app.MapPost("/api/auth/machine/login", (LoginRequest request, IAuthService authService) =>
                LoginMachineAsync(request, authService))
            .WithTags("Auth");
        app.MapPost("/api/auth/machine/refresh", (MachineRefreshRequest request, IAuthService authService) =>
                RefreshMachineAsync(request, authService))
            .WithTags("Auth");
        app.MapPost("/api/auth/machine/logout", (MachineLogoutRequest request, IAuthService authService) =>
                LogoutMachineAsync(request, authService))
            .WithTags("Auth");
        app.MapPost("/api/auth/access-tokens", (CreateMachinePatRequest request, IAuthService authService, ClaimsPrincipal claimsPrincipal) =>
                CreateMachinePatAsync(request, authService, claimsPrincipal))
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .WithTags("Auth");
        app.MapGet("/api/auth/access-tokens", (IAuthService authService, ClaimsPrincipal claimsPrincipal) =>
                ListMachinePatsAsync(authService, claimsPrincipal))
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .WithTags("Auth");
        app.MapDelete("/api/auth/access-tokens/{id:int}", (int id, IAuthService authService, ClaimsPrincipal claimsPrincipal) =>
                RevokeMachinePatAsync(id, authService, claimsPrincipal))
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .WithTags("Auth");

        app.MapGet("/api/auth/csrf", (IAuthHttpSessionService authHttpService, HttpRequest request, HttpResponse response) =>
                authHttpService.GetCsrf(request, response))
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .WithTags("Auth");
        app.MapGet("/api/auth/me", (IAuthHttpSessionService authHttpService, ClaimsPrincipal claimsPrincipal) =>
                authHttpService.GetMeAsync(claimsPrincipal))
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .WithTags("Auth");
        app.MapGet("/api/auth/bootstrap-status", (IAuthHttpSessionService authHttpService) =>
                authHttpService.GetBootstrapStatusAsync())
            .WithTags("Auth");

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

    private static async Task<IResult> CreateMachinePatAsync(
        CreateMachinePatRequest request,
        IAuthService authService,
        ClaimsPrincipal claimsPrincipal)
    {
        if (!TryGetUserId(claimsPrincipal, out var userId))
        {
            return ((ApiResult)ApiErrors.Unauthorized("Invalid identity context.")).ToHttpResult();
        }

        var result = await authService.CreateMachinePatAsync(userId, request);
        if (!result.Success || result.Data is null)
        {
            return result.ToHttpResult();
        }

        return ApiResults.Created(result.Data).ToHttpResult();
    }

    private static async Task<IResult> ListMachinePatsAsync(IAuthService authService, ClaimsPrincipal claimsPrincipal)
    {
        if (!TryGetUserId(claimsPrincipal, out var userId))
        {
            return ((ApiResult)ApiErrors.Unauthorized("Invalid identity context.")).ToHttpResult();
        }

        return (await authService.ListMachinePatsAsync(userId)).ToHttpResult();
    }

    private static async Task<IResult> RevokeMachinePatAsync(int id, IAuthService authService, ClaimsPrincipal claimsPrincipal)
    {
        if (!TryGetUserId(claimsPrincipal, out var userId))
        {
            return ((ApiResult)ApiErrors.Unauthorized("Invalid identity context.")).ToHttpResult();
        }

        return (await authService.RevokeMachinePatAsync(userId, id)).ToHttpResult();
    }

    private static bool TryGetUserId(ClaimsPrincipal claimsPrincipal, out int userId)
    {
        var userIdClaim = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out userId);
    }
}
