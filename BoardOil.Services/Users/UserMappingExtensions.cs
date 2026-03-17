using BoardOil.Abstractions.Entities;

namespace BoardOil.Services.Users;

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
