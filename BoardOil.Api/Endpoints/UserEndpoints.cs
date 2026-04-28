using System.Security.Claims;
using BoardOil.Api.Auth;
using BoardOil.Api.Extensions;
using BoardOil.Abstractions.Image;
using BoardOil.Abstractions.Users;
using BoardOil.Contracts.Contracts;
using BoardOil.Contracts.Users;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/users", (IUserService userService) =>
                userService.GetUsersAsync().ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .WithTags("Users");
        app.MapGet("/api/users/me/profile-image", async (ClaimsPrincipal user, IUserProfileImageService userProfileImageService) =>
            {
                if (!user.TryGetUserId(out var actorUserId))
                {
                    return ApiErrors.Unauthorized("Invalid identity context.").ToHttpResult();
                }

                return (await userProfileImageService.GetOwnProfileImageAsync(actorUserId)).ToHttpResult();
            })
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .WithTags("Users");
        app.MapPost("/api/users/me/profile-image", async (HttpRequest request, ClaimsPrincipal user, IUserProfileImageService userProfileImageService) =>
            {
                if (!user.TryGetUserId(out var actorUserId))
                {
                    return ApiErrors.Unauthorized("Invalid identity context.").ToHttpResult();
                }

                var uploadRequestResult = await TryReadUserProfileImageUploadRequestAsync(request);
                if (!uploadRequestResult.Success || uploadRequestResult.Data is null)
                {
                    return uploadRequestResult.ToHttpResult();
                }

                await using var contentStream = new MemoryStream(uploadRequestResult.Data.Content, writable: false);
                return (await userProfileImageService.UploadOwnProfileImageAsync(
                    actorUserId,
                    uploadRequestResult.Data.FileName,
                    uploadRequestResult.Data.ContentType,
                    contentStream))
                    .ToHttpResult();
            })
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .WithTags("Users");

        app.MapGet("/api/system/users", (IUserAdminService userAdminService) =>
                userAdminService.GetUsersAsync().ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System Users");
        app.MapPost("/api/system/users", (CreateUserRequest request, IUserAdminService userAdminService) =>
                userAdminService.CreateUserAsync(request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System Users");
        app.MapPut("/api/system/users/{id:int}", (int id, UpdateUserRequest request, IUserAdminService userAdminService) =>
                userAdminService.UpdateUserAsync(id, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System Users");
        app.MapPut("/api/system/users/{id:int}/password", (int id, ResetUserPasswordRequest request, IUserAdminService userAdminService) =>
                userAdminService.ResetUserPasswordAsync(id, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System Users");
        app.MapDelete("/api/system/users/{id:int}", async (int id, ClaimsPrincipal user, IUserAdminService userAdminService) =>
            {
                if (!user.TryGetUserId(out var actorUserId))
                {
                    return ApiErrors.Unauthorized("Invalid identity context.").ToHttpResult();
                }

                return (await userAdminService.DeleteUserAsync(id, actorUserId)).ToHttpResult();
            })
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System Users");

        return app;
    }

    private static async Task<ApiResult<UserProfileImageUploadRequest>> TryReadUserProfileImageUploadRequestAsync(HttpRequest request)
    {
        if (!request.HasFormContentType)
        {
            return ValidationFailure("file", "Image upload must use multipart/form-data.");
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

        return ApiResults.Ok(new UserProfileImageUploadRequest(
            imageFile.FileName,
            imageFile.ContentType,
            content));
    }

    private static ApiResult<UserProfileImageUploadRequest> ValidationFailure(string property, string message) =>
        ApiResults.BadRequest<UserProfileImageUploadRequest>(
            "Validation failed.",
            new Dictionary<string, string[]>
            {
                [property] = [message]
            });

    private sealed record UserProfileImageUploadRequest(string FileName, string ContentType, byte[] Content);
}
