using System.Security.Cryptography;
using System.Text;
using BoardOil.Abstractions.OAuth;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace BoardOil.Api.OAuth;

public sealed class OAuthTokenAuditPersistenceHandler(
    IOAuthTokenAuditService auditService,
    IHttpContextAccessor httpContextAccessor)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ApplyTokenResponseContext>
{
    public async ValueTask HandleAsync(OpenIddictServerEvents.ApplyTokenResponseContext context)
    {
        var request = context.Request;
        if (request is null
            || (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType()))
        {
            return;
        }

        context.Transaction.Properties.TryGetValue(
            OAuthTokenAuditPresentedTokenCaptureHandler.TransactionProperty,
            out var presentedTokenValue);
        var presentedToken = presentedTokenValue as OAuthTokenAuditPresentedTokenCapture;
        var httpContext = httpContextAccessor.HttpContext;
        var outcome = string.IsNullOrWhiteSpace(context.Error)
            ? OAuthTokenAuditOutcomes.Succeeded
            : OAuthTokenAuditOutcomes.Rejected;
        await auditService.RecordAsync(new OAuthTokenAuditInput(
            outcome,
            request.GrantType ?? string.Empty,
            context.Response.Error,
            context.Response.ErrorDescription,
            context.Response.ErrorUri,
            presentedToken?.TokenId,
            Fingerprint(GetPresentedToken(request)),
            Fingerprint(context.Response.RefreshToken),
            presentedToken?.AuthorizationId,
            presentedToken?.Subject,
            request.ClientId,
            httpContext?.TraceIdentifier,
            httpContext?.Request.Headers.UserAgent.ToString()));
    }

    private static string? GetPresentedToken(OpenIddictRequest request)
    {
        if (request.IsAuthorizationCodeGrantType())
        {
            return request.Code;
        }

        if (request.IsRefreshTokenGrantType())
        {
            return request.RefreshToken;
        }

        return null;
    }

    private static string? Fingerprint(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return $"sha256:{Convert.ToHexStringLower(hash)}";
    }
}
