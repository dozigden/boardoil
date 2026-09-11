using System.Text.Json;
using OpenIddict.Abstractions;

namespace BoardOil.Api.OAuth;

internal static class OAuthApplicationValidity
{
    public static async Task<bool> IsActiveAsync(IOpenIddictApplicationManager applications, object application,
        TimeProvider clock, CancellationToken cancellationToken = default)
    {
        var properties = await applications.GetPropertiesAsync(application, cancellationToken);
        if (!properties.TryGetValue(OAuthDynamicClientRegistrationService.DynamicRegistrationProperty, out var dynamicRegistration)
            || dynamicRegistration.ValueKind is not JsonValueKind.True) { return true; }
        if (!properties.TryGetValue(OAuthDynamicClientRegistrationService.RegistrationExpiresAtProperty, out var expiry)) { return true; }
        return expiry.ValueKind is JsonValueKind.String && expiry.TryGetDateTimeOffset(out var expiresAt)
            && expiresAt > clock.GetUtcNow();
    }
}
