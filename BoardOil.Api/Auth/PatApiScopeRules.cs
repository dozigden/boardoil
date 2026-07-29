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

        if (HttpMethods.IsPost(httpMethod) && IsCardSearchPath(path))
        {
            return MachinePatScopes.ApiRead;
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

    private static bool IsCardSearchPath(PathString path)
    {
        if (!path.StartsWithSegments(
            "/api/boards",
            StringComparison.OrdinalIgnoreCase,
            out var remainingPath))
        {
            return false;
        }

        var segments = remainingPath.Value?
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments is null || segments.Length != 3)
        {
            return false;
        }

        return IsBoardIdSegment(segments[0])
            && segments[1].Equals("cards", StringComparison.OrdinalIgnoreCase)
            && segments[2].Equals("search", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBoardIdSegment(string value) =>
        int.TryParse(value, out _)
        || value.Equals("{boardId}", StringComparison.OrdinalIgnoreCase)
        || value.Equals("{boardId:int}", StringComparison.OrdinalIgnoreCase);
}
