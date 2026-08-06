using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace BoardOil.Api.OAuth;

public sealed class OpenIddictConfigurationHandler(
    IHttpContextAccessor httpContextAccessor,
    OAuthEndpointUrlResolver urlResolver)
    : IOpenIddictServerHandler<OpenIddictServerEvents.HandleConfigurationRequestContext>
{
    public async ValueTask HandleAsync(OpenIddictServerEvents.HandleConfigurationRequestContext context)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("The OAuth discovery request has no HTTP context.");
        var publicBaseUrl = await urlResolver.GetPublicBaseUrlAsync(httpContext.Request);

        context.Issuer = new Uri($"{publicBaseUrl}/", UriKind.Absolute);
        context.AuthorizationEndpoint = new Uri($"{publicBaseUrl}/connect/authorize", UriKind.Absolute);
        context.TokenEndpoint = new Uri($"{publicBaseUrl}/connect/token", UriKind.Absolute);
        context.JsonWebKeySetEndpoint = new Uri($"{publicBaseUrl}/.well-known/jwks", UriKind.Absolute);
        context.Metadata["registration_endpoint"] = $"{publicBaseUrl}/connect/register";

        context.GrantTypes.Clear();
        context.GrantTypes.Add(OpenIddictConstants.GrantTypes.AuthorizationCode);
        context.GrantTypes.Add(OpenIddictConstants.GrantTypes.RefreshToken);
        context.ResponseTypes.Clear();
        context.ResponseTypes.Add(OpenIddictConstants.ResponseTypes.Code);
        context.ResponseModes.Clear();
        context.ResponseModes.Add(OpenIddictConstants.ResponseModes.Query);
        context.Scopes.Clear();
        context.Scopes.Add(BoardOil.Contracts.Auth.MachinePatScopes.McpRead);
        context.Scopes.Add(BoardOil.Contracts.Auth.MachinePatScopes.McpWrite);
        context.TokenEndpointAuthenticationMethods.Clear();
        context.TokenEndpointAuthenticationMethods.Add(OpenIddictConstants.ClientAuthenticationMethods.None);
        context.CodeChallengeMethods.Clear();
        context.CodeChallengeMethods.Add(OpenIddictConstants.CodeChallengeMethods.Sha256);
    }
}
