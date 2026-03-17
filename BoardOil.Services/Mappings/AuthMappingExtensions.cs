using BoardOil.Ef.Entities;
using BoardOil.Services.Contracts;

namespace BoardOil.Services.Mappings;

public static class AuthMappingExtensions
{
    public static AuthUserDto ToAuthUserDto(this BoardUser user) =>
        new(user.Id, user.UserName, user.Role.ToString());
}
