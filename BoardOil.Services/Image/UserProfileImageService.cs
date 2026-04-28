using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Image;
using BoardOil.Contracts.Contracts;
using BoardOil.Contracts.Users;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Persistence.Abstractions.Image;
using BoardOil.Persistence.Abstractions.Users;
using SixLabors.ImageSharp;

namespace BoardOil.Services.Image;

public sealed class UserProfileImageService(
    IUserRepository userRepository,
    IImageRepository imageRepository,
    IImageStorageService imageStorageService,
    IDbContextScopeFactory scopeFactory) : IUserProfileImageService
{
    private static readonly HashSet<string> AllowedContentTypes =
    [
        "image/png",
        "image/jpeg",
        "image/webp"
    ];

    public async Task<ApiResult<UserProfileImageDto>> GetOwnProfileImageAsync(int actorUserId)
    {
        using var scope = scopeFactory.CreateReadOnly();

        var user = userRepository.Get(actorUserId);
        if (user is null || !user.IsActive)
        {
            return ApiErrors.Unauthorized("User is not active.");
        }

        var existing = await imageRepository.GetLatestForEntityAsync(ImageEntityType.UserProfile, actorUserId);
        if (existing is null || existing.Width is null || existing.Height is null)
        {
            return ApiErrors.NotFound("User profile image was not found.");
        }

        return ToDto(existing);
    }

    public async Task<ApiResult<UserProfileImageDto>> UploadOwnProfileImageAsync(
        int actorUserId,
        string originalFileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.Create();

        var user = userRepository.Get(actorUserId);
        if (user is null || !user.IsActive)
        {
            return ApiErrors.Unauthorized("User is not active.");
        }

        var normalisedContentType = contentType.Trim().ToLowerInvariant();
        if (!AllowedContentTypes.Contains(normalisedContentType))
        {
            return ValidationFailure("file", "Supported image types are PNG, JPEG, and WEBP.");
        }

        byte[] payload;
        await using (var memoryStream = new MemoryStream())
        {
            await content.CopyToAsync(memoryStream, cancellationToken);
            payload = memoryStream.ToArray();
        }

        if (payload.Length == 0)
        {
            return ValidationFailure("file", "Image file cannot be empty.");
        }

        ImageInfo imageInfo;
        try
        {
            await using var identifyStream = new MemoryStream(payload, writable: false);
            imageInfo = await SixLabors.ImageSharp.Image.IdentifyAsync(identifyStream, cancellationToken)
                ?? throw new InvalidDataException("Unable to identify image.");
        }
        catch (UnknownImageFormatException)
        {
            return ValidationFailure("file", "Uploaded file is not a valid image.");
        }
        catch (InvalidImageContentException)
        {
            return ValidationFailure("file", "Uploaded image content is invalid.");
        }

        if (imageInfo.Width != imageInfo.Height)
        {
            return ValidationFailure("file", "User profile images must be square.");
        }

        await using var uploadStream = new MemoryStream(payload, writable: false);
        var saved = await imageStorageService.SaveAsync(new ImageStorageSaveRequest
        {
            EntityType = ImageStorageEntityType.UserProfile,
            EntityId = actorUserId,
            OriginalFileName = originalFileName,
            ContentType = normalisedContentType,
            Content = uploadStream,
        }, cancellationToken);

        var existing = await imageRepository.GetLatestForEntityAsync(ImageEntityType.UserProfile, actorUserId);
        var isCreate = existing is null;
        var oldRelativePath = existing?.RelativePath;
        var entity = existing ?? new EntityImage
        {
            EntityType = ImageEntityType.UserProfile,
            EntityId = actorUserId,
            CreatedAtUtc = saved.CreatedAtUtc,
        };

        entity.OriginalFileName = originalFileName;
        entity.ContentType = normalisedContentType;
        entity.RelativePath = saved.RelativePath;
        entity.ByteLength = saved.ByteLength;
        entity.Width = imageInfo.Width;
        entity.Height = imageInfo.Height;
        entity.UpdatedAtUtc = saved.UpdatedAtUtc;

        if (existing is null)
        {
            imageRepository.Add(entity);
        }

        await scope.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(oldRelativePath)
            && !string.Equals(oldRelativePath, entity.RelativePath, StringComparison.Ordinal))
        {
            await imageStorageService.DeleteIfExistsAsync(oldRelativePath, cancellationToken);
        }

        return isCreate ? ApiResults.Created(ToDto(entity)) : ApiResults.Ok(ToDto(entity));
    }

    public async Task<ApiResult> DeleteOwnProfileImageAsync(
        int actorUserId,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.Create();

        var user = userRepository.Get(actorUserId);
        if (user is null || !user.IsActive)
        {
            return ApiErrors.Unauthorized("User is not active.");
        }

        var existing = await imageRepository.GetLatestForEntityAsync(ImageEntityType.UserProfile, actorUserId);
        if (existing is null)
        {
            return ApiErrors.NotFound("User profile image was not found.");
        }

        var relativePath = existing.RelativePath;
        imageRepository.Remove(existing);
        await scope.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(relativePath))
        {
            await imageStorageService.DeleteIfExistsAsync(relativePath, cancellationToken);
        }

        return ApiResults.Ok();
    }

    private static ApiResult<UserProfileImageDto> ValidationFailure(string property, string message) =>
        ApiResults.BadRequest<UserProfileImageDto>(
            "Validation failed.",
            new Dictionary<string, string[]>
            {
                [property] = [message]
            });

    private static UserProfileImageDto ToDto(EntityImage entity) =>
        new(
            entity.Id,
            entity.ContentType,
            entity.RelativePath,
            entity.ByteLength,
            entity.Width ?? 0,
            entity.Height ?? 0,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);
}
