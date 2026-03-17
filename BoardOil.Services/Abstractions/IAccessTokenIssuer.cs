using BoardOil.Ef.Entities;

namespace BoardOil.Services.Abstractions;

public interface IAccessTokenIssuer
{
    string CreateAccessToken(BoardUser user, DateTime issuedAtUtc, DateTime expiresAtUtc);
}
