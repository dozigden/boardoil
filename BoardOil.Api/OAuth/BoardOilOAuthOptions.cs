namespace BoardOil.Api.OAuth;

public sealed class BoardOilOAuthOptions
{
    public TimeSpan DynamicClientRegistrationLifetime { get; init; } = TimeSpan.FromDays(90);
    public int DynamicClientRegistrationLimitPerMinute { get; init; } = 20;
}
