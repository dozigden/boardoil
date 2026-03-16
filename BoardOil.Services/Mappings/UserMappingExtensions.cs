using BoardOil.Ef.Entities;
using BoardOil.Services.Contracts;

namespace BoardOil.Services.Mappings;

public static class UserMappingExtensions
{
    public static ManagedUserDto ToManagedUserDto(this BoardUser user) =>
        new(
            user.Id,
            user.UserName,
            user.Role.ToString(),
            user.IsActive,
            user.CreatedAtUtc,
            user.UpdatedAtUtc);
}
