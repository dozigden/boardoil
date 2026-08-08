namespace BoardOil.Api.OAuth;

public sealed class BoardOilOAuthOptions
{
    public TimeSpan DynamicClientRegistrationLifetime { get; init; } = TimeSpan.FromDays(90);
    public int DynamicClientRegistrationLimitPerMinute { get; init; } = 20;
    public TimeSpan AuthorizationCodeLifetime { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(14);
    public TimeSpan RefreshTokenReuseLeeway { get; init; } = TimeSpan.FromSeconds(30);
}
