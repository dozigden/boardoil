using BoardOil.Contracts.Auth;
using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Services.Auth;

public static class AuthMappingExtensions
{
    public static AuthUserDto ToAuthUserDto(this EntityUser user) =>
        new(user.Id, user.UserName, user.Role.ToString());
}
