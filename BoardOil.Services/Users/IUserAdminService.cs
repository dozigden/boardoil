using BoardOil.Services.Contracts;

namespace BoardOil.Services.Users;

public interface IUserAdminService
{
    Task<ApiResult<IReadOnlyList<ManagedUserDto>>> GetUsersAsync();
    Task<ApiResult<ManagedUserDto>> CreateUserAsync(CreateUserRequest request);
    Task<ApiResult<ManagedUserDto>> UpdateUserRoleAsync(int id, UpdateUserRoleRequest request);
    Task<ApiResult<ManagedUserDto>> UpdateUserStatusAsync(int id, UpdateUserStatusRequest request);
}
