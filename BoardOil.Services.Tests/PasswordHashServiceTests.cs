using BoardOil.Services.Auth;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class PasswordHashServiceTests
{
    [Fact]
    public void HashPassword_AndVerify_ReturnsTrueForOriginalPassword()
    {
        var service = new PasswordHashService();
        var hash = service.HashPassword("super-secret");

        var isValid = service.VerifyPassword("super-secret", hash);

        Assert.True(isValid);
    }

    [Fact]
    public void VerifyPassword_ReturnsFalseForDifferentPassword()
    {
        var service = new PasswordHashService();
        var hash = service.HashPassword("super-secret");

        var isValid = service.VerifyPassword("wrong-password", hash);

        Assert.False(isValid);
    }
}
