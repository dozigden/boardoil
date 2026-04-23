using BoardOil.Contracts.Contracts;
using BoardOil.Contracts.Users;

namespace BoardOil.Abstractions.Users;

public interface IUserAdminService
{
    Task<ApiResult<IReadOnlyList<ManagedUserDto>>> GetUsersAsync();
    Task<ApiResult<ManagedUserDto>> CreateUserAsync(CreateUserRequest request);
    Task<ApiResult<ManagedUserDto>> UpdateUserAsync(int id, UpdateUserRequest request);
    Task<ApiResult> ResetUserPasswordAsync(int id, ResetUserPasswordRequest request);
    Task<ApiResult> DeleteUserAsync(int id, int actorUserId);
}
