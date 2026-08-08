using OpenIddict.Server;

namespace BoardOil.Api.OAuth;

public sealed class OpenIddictPublicBaseUriHandler(
    IHttpContextAccessor httpContextAccessor,
    OAuthEndpointUrlResolver urlResolver)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessRequestContext>
{
    public async ValueTask HandleAsync(OpenIddictServerEvents.ProcessRequestContext context)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("The OAuth request has no HTTP context.");
        var publicBaseUrl = await urlResolver.GetPublicBaseUrlAsync(httpContext.Request);

        context.BaseUri = new Uri($"{publicBaseUrl}/", UriKind.Absolute);
    }
}
