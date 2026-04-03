using BoardOil.Contracts.Contracts;
using BoardOil.Contracts.Users;

namespace BoardOil.Abstractions.Users;

public interface IUserService
{
    Task<ApiResult<IReadOnlyList<UserDirectoryEntryDto>>> GetUsersAsync();
}
