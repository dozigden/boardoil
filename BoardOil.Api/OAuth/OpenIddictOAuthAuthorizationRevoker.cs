using BoardOil.Abstractions.OAuth;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BoardOil.Api.OAuth;

public sealed class OpenIddictOAuthAuthorizationRevoker(
    IOpenIddictAuthorizationManager authorizationManager) : IOAuthAuthorizationRevoker
{
    public async Task RevokeAsync(string authorizationId, CancellationToken cancellationToken = default)
    {
        var authorization = await authorizationManager.FindByIdAsync(authorizationId, cancellationToken);
        if (authorization is null
            || await authorizationManager.HasStatusAsync(authorization, Statuses.Revoked, cancellationToken))
        {
            return;
        }

        if (await authorizationManager.TryRevokeAsync(authorization, cancellationToken))
        {
            return;
        }

        authorization = await authorizationManager.FindByIdAsync(authorizationId, cancellationToken);
        if (authorization is null
            || await authorizationManager.HasStatusAsync(authorization, Statuses.Revoked, cancellationToken))
        {
            return;
        }

        throw new InvalidOperationException("The OpenIddict authorization could not be revoked.");
    }
}
