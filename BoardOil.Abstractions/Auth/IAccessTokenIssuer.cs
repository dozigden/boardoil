namespace BoardOil.Abstractions.Auth;

public interface IAccessTokenIssuer
{
    string CreateAccessToken(int userId, string userName, string role, DateTime issuedAtUtc, DateTime expiresAtUtc);
}
