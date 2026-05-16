using BoardOil.Contracts.Users;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Services.Users;

public static class UserMappingExtensions
{
    public static ManagedUserDto ToManagedUserDto(this EntityUser user, string? profileImageRelativePath = null) =>
        new(
            user.Id,
            user.UserName,
            user.DisplayName,
            user.Email,
            user.Role.ToString(),
            user.IdentityType.ToString(),
            profileImageRelativePath,
            user.IsActive,
            user.CreatedAtUtc,
            user.UpdatedAtUtc);

    public static ClientAccountDto ToClientAccountDto(this EntityUser user, string? profileImageRelativePath = null) =>
        new(
            user.Id,
            user.UserName,
            user.DisplayName,
            user.Email,
            user.Role.ToString(),
            profileImageRelativePath,
            user.IsActive,
            user.CreatedAtUtc,
            user.UpdatedAtUtc);
}
