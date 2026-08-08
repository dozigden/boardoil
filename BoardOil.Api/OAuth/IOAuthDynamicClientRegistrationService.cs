namespace BoardOil.Api.OAuth;

public interface IOAuthDynamicClientRegistrationService
{
    Task<OAuthDynamicClientRegistrationResult> RegisterAsync(
        OAuthDynamicClientRegistrationRequest request,
        CancellationToken cancellationToken = default);

    Task<int> CleanupExpiredRegistrationsAsync(CancellationToken cancellationToken = default);
}
