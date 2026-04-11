using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Users;
using BoardOil.Contracts.Contracts;
using BoardOil.Contracts.Users;
using BoardOil.Persistence.Abstractions.Users;

namespace BoardOil.Services.Users;

public sealed class UserService(
    IUserRepository userRepository,
    IDbContextScopeFactory scopeFactory) : IUserService
{
    public async Task<ApiResult<IReadOnlyList<UserDirectoryEntryDto>>> GetUsersAsync()
    {
        using var scope = scopeFactory.CreateReadOnly();

        var users = (await userRepository.GetUsersOrderedAsync())
            .Select(x => new UserDirectoryEntryDto(x.Id, x.UserName, x.IsActive))
            .ToList();

        return users;
    }
}
