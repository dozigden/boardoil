using BoardOil.Api.Auth;
using BoardOil.Api.Configuration;
using BoardOil.Contracts.Common;
using Microsoft.AspNetCore.Antiforgery;

namespace BoardOil.Api.Middleware;

public sealed class ApiAntiforgeryMiddleware(RequestDelegate next, CsrfOptions options)
{
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (!RequiresValidation(context))
        {
            await next(context);
            return;
        }

        // API callers send the request token in a header. Reject its absence
        // before the framework can fall back to reading a multipart form body.
        if (!context.Request.Headers.TryGetValue(options.HeaderName, out var token)
            || string.IsNullOrWhiteSpace(token.ToString()))
        {
            await WriteFailureAsync(context);
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            await WriteFailureAsync(context);
            return;
        }

        await next(context);
    }

    private static bool RequiresValidation(HttpContext context)
    {
        var request = context.Request;
        if (!HttpMethods.IsPost(request.Method)
            && !HttpMethods.IsPut(request.Method)
            && !HttpMethods.IsPatch(request.Method)
            && !HttpMethods.IsDelete(request.Method))
        {
            return false;
        }

        if (!request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            || IsExemptAuthPath(request.Path)
            || context.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var authType = context.User.FindFirst("boardoil_auth_type")?.Value;
        return !string.Equals(authType, "pat", StringComparison.Ordinal)
            && !JwtAuthenticationSourceContext.IsValidatedBearerHeader(context);
    }

    private static bool IsExemptAuthPath(PathString path) =>
        path.StartsWithSegments("/api/auth/register-initial-admin", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/api/auth/login", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/api/auth/refresh", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/api/auth/logout", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/api/auth/machine/login", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/api/auth/machine/refresh", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/api/auth/machine/logout", StringComparison.OrdinalIgnoreCase);

    private static Task WriteFailureAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return context.Response.WriteAsJsonAsync(new ApiResult(false, 403, "CSRF validation failed."));
    }
}
