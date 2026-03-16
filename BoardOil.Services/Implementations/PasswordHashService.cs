using BoardOil.Services.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace BoardOil.Services.Implementations;

public sealed class PasswordHashService : IPasswordHashService
{
    private readonly PasswordHasher<string> _hasher = new();

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return _hasher.HashPassword(string.Empty, password);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var result = _hasher.VerifyHashedPassword(string.Empty, passwordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
