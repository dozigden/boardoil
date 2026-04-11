using BoardOil.Contracts.Auth;
using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Services.Auth;

internal static class MachinePatRules
{
    internal static readonly string[] SupportedScopes =
    [
        MachinePatScopes.McpRead,
        MachinePatScopes.McpWrite,
        MachinePatScopes.ApiRead,
        MachinePatScopes.ApiWrite,
        MachinePatScopes.ApiAdmin,
        MachinePatScopes.ApiSystem
    ];

    internal static readonly string[] SupportedUserScopes =
    [
        MachinePatScopes.McpRead,
        MachinePatScopes.McpWrite
    ];

    internal static readonly string[] SupportedClientScopes = SupportedScopes;

    internal static readonly string[] DefaultUserScopes =
    [
        MachinePatScopes.McpRead,
        MachinePatScopes.McpWrite
    ];

    internal static readonly string[] DefaultClientScopes =
    [
        MachinePatScopes.ApiRead,
        MachinePatScopes.ApiWrite,
        MachinePatScopes.ApiAdmin,
        MachinePatScopes.ApiSystem
    ];

    internal static IReadOnlyList<string> ParseScopes(string scopesCsv)
    {
        var normalisedScopes = scopesCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);

        return SupportedScopes
            .Where(scope => normalisedScopes.Contains(scope))
            .ToArray();
    }

    internal static IReadOnlyList<string> NormaliseScopes(IEnumerable<string>? scopes, IEnumerable<string> defaultScopes)
        => NormaliseScopes(scopes, defaultScopes, SupportedScopes);

    internal static IReadOnlyList<string> NormaliseScopes(
        IEnumerable<string>? scopes,
        IEnumerable<string> defaultScopes,
        IEnumerable<string> supportedScopes)
    {
        var normalisedScopes = (scopes ?? defaultScopes)
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);

        return supportedScopes
            .Where(scope => normalisedScopes.Contains(scope))
            .ToArray();
    }

    internal static MachinePatDto ToMachinePatDto(EntityPersonalAccessToken token)
    {
        return new MachinePatDto(
            token.Id,
            token.Name,
            token.TokenPrefix,
            ParseScopes(token.ScopesCsv),
            token.CreatedAtUtc,
            token.ExpiresAtUtc,
            token.LastUsedAtUtc,
            token.RevokedAtUtc);
    }
}
