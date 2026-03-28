namespace BoardOil.Contracts.Auth;

public sealed record RegisterInitialAdminRequest(string UserName, string Password);

public sealed record LoginRequest(string UserName, string Password);

public sealed record MachineRefreshRequest(string RefreshToken);

public sealed record MachineLogoutRequest(string? RefreshToken);

public sealed record MachinePatLoginRequest(string Token);

public sealed record CreateMachinePatRequest(
    string Name,
    int? ExpiresInDays = null,
    string[]? Scopes = null);

public sealed record AuthUserDto(int Id, string UserName, string Role);

public sealed record AuthSessionDto(
    AuthUserDto User,
    DateTime AccessTokenExpiresAtUtc,
    DateTime RefreshTokenExpiresAtUtc,
    string CsrfToken);

public sealed record CsrfTokenDto(string CsrfToken);

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
    string CsrfToken,
    AuthUserDto User)
{
    public AuthSessionDto ToDto() =>
        new(User, AccessTokenExpiresAtUtc, RefreshTokenExpiresAtUtc, CsrfToken);

    public MachineAuthSessionDto ToMachineDto() =>
        new(AccessToken, AccessTokenExpiresAtUtc, RefreshToken, RefreshTokenExpiresAtUtc, User);
}
