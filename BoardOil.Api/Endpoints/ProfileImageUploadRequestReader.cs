using BoardOil.Contracts.Common;
using BoardOil.Contracts.Users;

namespace BoardOil.Api.Endpoints;

internal static class ProfileImageUploadRequestReader
{
    public const long MaxRequestBodyLength = ProfileImageUploadConstraints.MaxByteLength + (64 * 1024);

    public static async Task<ApiResult<ProfileImageUploadRequest>> TryReadAsync(HttpRequest request)
    {
        if (!request.HasFormContentType)
        {
            return ValidationFailure("file", "Image upload must use multipart/form-data.");
        }

        if (request.ContentLength > MaxRequestBodyLength)
        {
            return ValidationFailure("file", $"Profile image must be {ProfileImageUploadConstraints.MaxByteLength / (1024 * 1024)} MB or smaller.");
        }

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync();
        }
        catch (InvalidDataException)
        {
            return ValidationFailure("file", "Image upload could not be read.");
        }

        var imageFile = form.Files.GetFile("file");
        if (imageFile is null)
        {
            return ValidationFailure("file", "Image file is required.");
        }

        if (imageFile.Length <= 0)
        {
            return ValidationFailure("file", "Image file cannot be empty.");
        }

        if (imageFile.Length > ProfileImageUploadConstraints.MaxByteLength)
        {
            return ValidationFailure("file", $"Profile image must be {ProfileImageUploadConstraints.MaxByteLength / (1024 * 1024)} MB or smaller.");
        }

        if (string.IsNullOrWhiteSpace(imageFile.ContentType))
        {
            return ValidationFailure("file", "Image content type is required.");
        }

        byte[] content;
        await using (var fileStream = imageFile.OpenReadStream())
        {
            using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream);
            content = memoryStream.ToArray();
        }

        return ApiResults.Ok(new ProfileImageUploadRequest(
            imageFile.FileName,
            imageFile.ContentType,
            content));
    }

    private static ApiResult<ProfileImageUploadRequest> ValidationFailure(string property, string message) =>
        ApiResults.BadRequest<ProfileImageUploadRequest>(
            "Validation failed.",
            new Dictionary<string, string[]>
            {
                [property] = [message]
            });
}

internal sealed record ProfileImageUploadRequest(string FileName, string ContentType, byte[] Content);
