using BoardOil.Abstractions.Entities;
using BoardOil.Contracts.Auth;

namespace BoardOil.Services.Auth;

public static class AuthMappingExtensions
{
    public static AuthUserDto ToAuthUserDto(this BoardUser user) =>
        new(user.Id, user.UserName, user.Role.ToString());
}
