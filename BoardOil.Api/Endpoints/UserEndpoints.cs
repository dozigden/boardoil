using System.Security.Claims;
using BoardOil.Api.Auth;
using BoardOil.Api.Extensions;
using BoardOil.Abstractions.Image;
using BoardOil.Abstractions.Users;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.Users;
using BoardOil.Services.Auth;
using Microsoft.AspNetCore.Mvc;

namespace BoardOil.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/users", (IUserService userService) =>
                userService.GetUsersAsync().ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .WithTags("Users");
        app.MapGet("/api/users/me", async (ClaimsPrincipal user, IUserService userService) =>
            {
                if (!user.TryGetUserId(out var actorUserId))
                {
                    return ApiErrors.Unauthorized("Invalid identity context.").ToHttpResult();
                }

                return (await userService.GetOwnProfileAsync(actorUserId)).ToHttpResult();
            })
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .WithTags("Users");
        app.MapPut("/api/users/me", async (UpdateOwnUserProfileRequest request, ClaimsPrincipal user, IUserService userService) =>
            {
                if (!user.TryGetUserId(out var actorUserId))
                {
                    return ApiErrors.Unauthorized("Invalid identity context.").ToHttpResult();
                }

                return (await userService.UpdateOwnProfileAsync(actorUserId, request)).ToHttpResult();
            })
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

                var uploadRequestResult = await ProfileImageUploadRequestReader.TryReadAsync(request);
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
            .WithMetadata(new RequestSizeLimitAttribute(ProfileImageUploadRequestReader.MaxRequestBodyLength))
            .WithMetadata(new RequestFormLimitsAttribute
            {
                MultipartBodyLengthLimit = ProfileImageUploadRequestReader.MaxRequestBodyLength
            })
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .WithTags("Users");
        app.MapDelete("/api/users/me/profile-image", async (ClaimsPrincipal user, IUserProfileImageService userProfileImageService) =>
            {
                if (!user.TryGetUserId(out var actorUserId))
                {
                    return ApiErrors.Unauthorized("Invalid identity context.").ToHttpResult();
                }

                return (await userProfileImageService.DeleteOwnProfileImageAsync(actorUserId)).ToHttpResult();
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
}
