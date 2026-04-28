using BoardOil.Contracts.Contracts;
using BoardOil.Contracts.Users;

namespace BoardOil.Abstractions.Image;

public interface IUserProfileImageService
{
    Task<ApiResult<UserProfileImageDto>> GetOwnProfileImageAsync(int actorUserId);
    Task<ApiResult<UserProfileImageDto>> UploadOwnProfileImageAsync(
        int actorUserId,
        string originalFileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default);
}
