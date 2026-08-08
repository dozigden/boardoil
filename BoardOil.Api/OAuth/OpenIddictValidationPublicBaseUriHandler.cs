using OpenIddict.Validation;

namespace BoardOil.Api.OAuth;

public sealed class OpenIddictValidationPublicBaseUriHandler(
    IHttpContextAccessor httpContextAccessor,
    OAuthEndpointUrlResolver urlResolver)
    : IOpenIddictValidationHandler<OpenIddictValidationEvents.ProcessRequestContext>
{
    public async ValueTask HandleAsync(OpenIddictValidationEvents.ProcessRequestContext context)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("The OAuth resource request has no HTTP context.");
        var publicBaseUrl = await urlResolver.GetPublicBaseUrlAsync(httpContext.Request);

        context.BaseUri = new Uri($"{publicBaseUrl}/", UriKind.Absolute);
    }
}
