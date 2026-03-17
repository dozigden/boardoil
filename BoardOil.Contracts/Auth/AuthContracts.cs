namespace BoardOil.Contracts.Auth;

public sealed record RegisterInitialAdminRequest(string UserName, string Password);

public sealed record LoginRequest(string UserName, string Password);

public sealed record AuthUserDto(int Id, string UserName, string Role);

public sealed record AuthSessionDto(
    AuthUserDto User,
    DateTime AccessTokenExpiresAtUtc,
    DateTime RefreshTokenExpiresAtUtc,
    string CsrfToken);

public sealed record CsrfTokenDto(string CsrfToken);

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
}
