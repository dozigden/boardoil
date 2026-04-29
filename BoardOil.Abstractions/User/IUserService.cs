using BoardOil.Contracts.Contracts;
using BoardOil.Contracts.Users;

namespace BoardOil.Abstractions.Users;

public interface IUserService
{
    Task<ApiResult<IReadOnlyList<UserDirectoryEntryDto>>> GetUsersAsync();
    Task<ApiResult<OwnUserProfileDto>> GetOwnProfileAsync(int actorUserId);
    Task<ApiResult<OwnUserProfileDto>> UpdateOwnProfileAsync(int actorUserId, UpdateOwnUserProfileRequest request);
}
