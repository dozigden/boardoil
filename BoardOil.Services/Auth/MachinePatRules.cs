using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Services.Auth;

internal static class MachinePatRules
{
    internal static readonly string[] SupportedScopes =
    [
        MachinePatScopes.McpRead,
        MachinePatScopes.McpWrite,
        MachinePatScopes.McpDiagnosticsRead,
        MachinePatScopes.ApiRead,
        MachinePatScopes.ApiWrite,
        MachinePatScopes.ApiAdmin,
        MachinePatScopes.ApiSystem
    ];

    internal static readonly string[] SupportedUserScopes =
    [
        MachinePatScopes.McpRead,
        MachinePatScopes.McpWrite,
        MachinePatScopes.McpDiagnosticsRead
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

    internal static IReadOnlyList<ValidationError> ValidateCreation(
        CreateMachinePatRequest request,
        IEnumerable<string> defaultScopes,
        IEnumerable<string> supportedScopes,
        string nameField = "name")
    {
        var errors = new List<ValidationError>();
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(new ValidationError(nameField, "Token name is required."));
        }
        else if (name.Length > 120)
        {
            errors.Add(new ValidationError(nameField, "Token name must be 120 characters or fewer."));
        }

        if (request.ExpiresInDays is <= 0 or > 3650)
        {
            errors.Add(new ValidationError("expiresInDays", "Expiry must be between 1 and 3650 days."));
        }

        var requestedScopes = (request.Scopes ?? defaultScopes)
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        if (requestedScopes.Length == 0)
        {
            errors.Add(new ValidationError("scopes", "At least one scope is required."));
        }
        else if (requestedScopes.Except(supportedScopes, StringComparer.OrdinalIgnoreCase).Any())
        {
            errors.Add(new ValidationError("scopes", "Unsupported scope provided."));
        }

        return errors;
    }

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
