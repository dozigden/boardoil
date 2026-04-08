using System.Security.Cryptography;
using System.Text;
using BoardOil.Abstractions.Auth;

namespace BoardOil.Api.Tests.Infrastructure;

internal sealed class FastPasswordHashService : IPasswordHashService
{
    private const string Prefix = "test-hash-v1:";

    public string HashPassword(string password)
    {
        var payload = Encoding.UTF8.GetBytes($"boardoil-test::{password}");
        var hash = SHA256.HashData(payload);
        return Prefix + Convert.ToHexString(hash);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (!passwordHash.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        return string.Equals(HashPassword(password), passwordHash, StringComparison.Ordinal);
    }
}
