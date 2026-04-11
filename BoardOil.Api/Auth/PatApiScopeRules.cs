using BoardOil.Contracts.Auth;
using Microsoft.AspNetCore.Http;

namespace BoardOil.Api.Auth;

internal static class PatApiScopeRules
{
    internal static string GetRequiredScope(HttpRequest request) =>
        GetRequiredScope(request.Method, request.Path);

    internal static string GetRequiredScope(string httpMethod, PathString path)
    {
        if (IsSystemPath(path))
        {
            return MachinePatScopes.ApiSystem;
        }

        if (IsAdminPath(path))
        {
            return MachinePatScopes.ApiAdmin;
        }

        if (HttpMethods.IsGet(httpMethod) || HttpMethods.IsHead(httpMethod))
        {
            return MachinePatScopes.ApiRead;
        }

        return MachinePatScopes.ApiWrite;
    }

    internal static bool IsSystemPath(PathString path) =>
        path.StartsWithSegments("/api/system", StringComparison.OrdinalIgnoreCase);

    internal static bool IsAdminPath(PathString path) =>
        path.StartsWithSegments("/api/admin", StringComparison.OrdinalIgnoreCase);
}
