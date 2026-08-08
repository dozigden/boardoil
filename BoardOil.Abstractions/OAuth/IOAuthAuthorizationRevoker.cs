namespace BoardOil.Abstractions.OAuth;

public interface IOAuthAuthorizationRevoker
{
    Task RevokeAsync(string authorizationId, CancellationToken cancellationToken = default);
}
