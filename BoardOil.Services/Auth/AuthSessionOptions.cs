namespace BoardOil.Services.Auth;

public sealed class AuthSessionOptions
{
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 14;
}
