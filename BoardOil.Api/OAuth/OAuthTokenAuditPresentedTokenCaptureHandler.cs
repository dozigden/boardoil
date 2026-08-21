using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BoardOil.Api.OAuth;

public sealed class OAuthTokenAuditPresentedTokenCaptureHandler
    : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenContext>
{
    internal const string TransactionProperty = "boardoil:oauth_token_audit_presented_token";

    public ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenContext context)
    {
        var request = context.Transaction.Request;
        if (request is null
            || (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType()))
        {
            return default;
        }

        context.Transaction.Properties[TransactionProperty] = new OAuthTokenAuditPresentedTokenCapture(
            context.TokenId,
            context.AuthorizationId,
            context.Principal?.GetClaim(Claims.Subject));
        return default;
    }
}

internal sealed record OAuthTokenAuditPresentedTokenCapture(
    string? TokenId,
    string? AuthorizationId,
    string? Subject);
