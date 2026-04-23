using BoardOil.Contracts.Users;
using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Services.Users;

public static class UserMappingExtensions
{
    public static ManagedUserDto ToManagedUserDto(this EntityUser user) =>
        new(
            user.Id,
            user.UserName,
            user.Email,
            user.Role.ToString(),
            user.IdentityType.ToString(),
            user.IsActive,
            user.CreatedAtUtc,
            user.UpdatedAtUtc);

    public static ClientAccountDto ToClientAccountDto(this EntityUser user) =>
        new(
            user.Id,
            user.UserName,
            user.Email,
            user.Role.ToString(),
            user.IsActive,
            user.CreatedAtUtc,
            user.UpdatedAtUtc);
}
