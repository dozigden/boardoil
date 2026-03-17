using BoardOil.Abstractions.Entities;

namespace BoardOil.Abstractions.Auth;

public interface IAccessTokenIssuer
{
    string CreateAccessToken(BoardUser user, DateTime issuedAtUtc, DateTime expiresAtUtc);
}
