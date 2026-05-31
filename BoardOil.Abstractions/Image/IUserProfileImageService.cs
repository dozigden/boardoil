using BoardOil.Contracts.Common;
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
    Task<ApiResult> DeleteOwnProfileImageAsync(
        int actorUserId,
        CancellationToken cancellationToken = default);
    Task<ApiResult<UserProfileImageDto>> UploadClientAccountProfileImageAsync(
        int clientAccountId,
        string originalFileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteClientAccountProfileImageAsync(
        int clientAccountId,
        CancellationToken cancellationToken = default);
}
