using BoardOil.Abstractions.Entities;

namespace BoardOil.Abstractions.Auth;

public interface IAccessTokenIssuer
{
    string CreateAccessToken(int userId, string userName, UserRole role, DateTime issuedAtUtc, DateTime expiresAtUtc);
}
