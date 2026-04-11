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

    internal static readonly string[] SupportedBoardAccessModes =
    [
        MachinePatBoardAccessModes.All,
        MachinePatBoardAccessModes.Selected
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

    internal static string NormaliseBoardAccessMode(string? boardAccessMode)
    {
        if (string.IsNullOrWhiteSpace(boardAccessMode))
        {
            return MachinePatBoardAccessModes.All;
        }

        return boardAccessMode.Trim().ToLowerInvariant();
    }

    internal static IReadOnlyList<int> ParseAllowedBoardIds(string allowedBoardIdsCsv) =>
        allowedBoardIdsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var boardId) ? boardId : (int?)null)
            .Where(x => x is > 0)
            .Select(x => x!.Value)
            .Distinct()
            .Order()
            .ToArray();

    internal static IReadOnlyList<int> NormaliseAllowedBoardIds(IEnumerable<int>? allowedBoardIds) =>
        (allowedBoardIds ?? [])
            .Where(x => x > 0)
            .Distinct()
            .Order()
            .ToArray();

    internal static MachinePatDto ToMachinePatDto(EntityPersonalAccessToken token)
    {
        var boardAccessMode = NormaliseBoardAccessMode(token.BoardAccessMode);
        var allowedBoardIds = boardAccessMode == MachinePatBoardAccessModes.All
            ? Array.Empty<int>()
            : ParseAllowedBoardIds(token.AllowedBoardIdsCsv);

        return new MachinePatDto(
            token.Id,
            token.Name,
            token.TokenPrefix,
            ParseScopes(token.ScopesCsv),
            boardAccessMode,
            allowedBoardIds,
            token.CreatedAtUtc,
            token.ExpiresAtUtc,
            token.LastUsedAtUtc,
            token.RevokedAtUtc);
    }
}
