using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace BoardOil.Api.OAuth;

public sealed class OAuthTokenAuditPresentedTokenCaptureHandler(
    OAuthTokenAuditCaptureState captureState)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenContext>
{
    internal const string TransactionProperty = "boardoil:oauth_token_audit_presented_token";

    public ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenContext context)
    {
        if (!captureState.IsEnabled)
        {
            return default;
        }

        var request = context.Transaction.Request;
        if (request is null
            || (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType()))
        {
            return default;
        }

        context.Transaction.Properties[TransactionProperty] = new OAuthTokenAuditPresentedTokenCapture(
            context.AuthorizationId);
        return default;
    }
}

internal sealed record OAuthTokenAuditPresentedTokenCapture(
    string? AuthorizationId);
