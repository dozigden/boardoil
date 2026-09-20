namespace BoardOil.Contracts.Auth;

public sealed record RegisterInitialAdminRequest(string UserName, string Email, string Password);

public sealed record LoginRequest(string UserName, string Password);
public sealed record ChangeOwnPasswordRequest(string CurrentPassword, string NewPassword);

public sealed record MachineRefreshRequest(string RefreshToken);

public sealed record MachineLogoutRequest(string? RefreshToken);

public static class MachinePatScopes
{
    public const string McpRead = "mcp:read";
    public const string McpWrite = "mcp:write";
    public const string ApiRead = "api:read";
    public const string ApiWrite = "api:write";
    public const string ApiAdmin = "api:admin";
    public const string ApiSystem = "api:system";
}

public static class McpScopeRules
{
    public static bool Allows(IEnumerable<string>? grantedScopes, string? requiredScope)
    {
        if (string.IsNullOrWhiteSpace(requiredScope))
        {
            return true;
        }

        if (grantedScopes is null)
        {
            return false;
        }

        if (grantedScopes.Contains(requiredScope, StringComparer.Ordinal))
        {
            return true;
        }

        return string.Equals(requiredScope, MachinePatScopes.McpRead, StringComparison.Ordinal)
            && grantedScopes.Contains(MachinePatScopes.McpWrite, StringComparer.Ordinal);
    }
}

public sealed record CreateMachinePatRequest(
    string Name,
    int? ExpiresInDays = null,
    string[]? Scopes = null);

public sealed record AuthUserDto(int Id, string UserName, string DisplayName, string Role);

public sealed record AuthSessionDto(
    AuthUserDto User,
    DateTime AccessTokenExpiresAtUtc,
    DateTime RefreshTokenExpiresAtUtc);

public sealed record CsrfTokenDto(string CsrfToken, int UserId);

public sealed record MachineAuthSessionDto(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    AuthUserDto User,
    string TokenType = "Bearer");

public sealed record MachinePatDto(
    int Id,
    string Name,
    string TokenPrefix,
    IReadOnlyList<string> Scopes,
    DateTime CreatedAtUtc,
    DateTime? ExpiresAtUtc,
    DateTime? LastUsedAtUtc,
    DateTime? RevokedAtUtc);

public sealed record CreatedMachinePatDto(
    MachinePatDto Token,
    string PlainTextToken);

public sealed record BootstrapStatusDto(bool RequiresInitialAdminSetup);

public sealed record AuthSessionTokens(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    AuthUserDto User)
{
    public AuthSessionDto ToDto() =>
        new(User, AccessTokenExpiresAtUtc, RefreshTokenExpiresAtUtc);

    public MachineAuthSessionDto ToMachineDto() =>
        new(AccessToken, AccessTokenExpiresAtUtc, RefreshToken, RefreshTokenExpiresAtUtc, User);
}
