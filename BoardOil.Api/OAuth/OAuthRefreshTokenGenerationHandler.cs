using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BoardOil.Api.OAuth;

public sealed class OAuthRefreshTokenGenerationHandler(
    IOpenIddictApplicationManager applicationManager)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
{
    public async ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        if ((!context.Request.IsAuthorizationCodeGrantType()
                && !context.Request.IsRefreshTokenGrantType())
            || string.IsNullOrWhiteSpace(context.Request.ClientId))
        {
            return;
        }

        var application = await applicationManager.FindByClientIdAsync(
            context.Request.ClientId,
            context.CancellationToken);
        if (application is null
            || !await applicationManager.HasPermissionAsync(
                application,
                Permissions.GrantTypes.RefreshToken,
                context.CancellationToken))
        {
            return;
        }

        context.GenerateRefreshToken = true;
        context.IncludeRefreshToken = true;
    }
}
