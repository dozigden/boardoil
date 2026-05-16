using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Users;
using BoardOil.Contracts.Contracts;
using BoardOil.Contracts.Users;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Users;

namespace BoardOil.Services.Users;

public sealed class UserService(
    IUserRepository userRepository,
    IDbContextScopeFactory scopeFactory) : IUserService
{
    public async Task<ApiResult<IReadOnlyList<UserDirectoryEntryDto>>> GetUsersAsync()
    {
        using var scope = scopeFactory.CreateReadOnly();

        var users = (await userRepository.GetUsersOrderedAsync())
            .Select(x => new UserDirectoryEntryDto(x.Id, x.UserName, x.DisplayName, x.IsActive))
            .ToList();

        return users;
    }

    public async Task<ApiResult<OwnUserProfileDto>> GetOwnProfileAsync(int actorUserId)
    {
        using var scope = scopeFactory.CreateReadOnly();

        var user = userRepository.Get(actorUserId);
        if (user is null || !user.IsActive)
        {
            return ApiErrors.Unauthorized("User is not active.");
        }

        if (user.IdentityType == UserIdentityType.Client)
        {
            return ApiErrors.Forbidden("Client accounts cannot use this endpoint.");
        }

        return new OwnUserProfileDto(
            user.Id,
            user.UserName,
            user.DisplayName,
            user.Email,
            user.Role.ToString());
    }

    public async Task<ApiResult<OwnUserProfileDto>> UpdateOwnProfileAsync(int actorUserId, UpdateOwnUserProfileRequest request)
    {
        using var scope = scopeFactory.Create();

        var user = userRepository.Get(actorUserId);
        if (user is null || !user.IsActive)
        {
            return ApiErrors.Unauthorized("User is not active.");
        }

        if (user.IdentityType == UserIdentityType.Client)
        {
            return ApiErrors.Forbidden("Client accounts cannot use this endpoint.");
        }

        var validationErrors = ValidateDisplayNameAndEmail(request.DisplayName, request.Email);
        if (validationErrors.Count > 0)
        {
            return ApiErrors.ValidationFailed(validationErrors);
        }

        var normalisedEmail = EmailAddressRules.TryNormalise(request.Email)!;
        var emailExists = await userRepository.NormalisedEmailExistsForOtherUserAsync(user.Id, normalisedEmail);
        if (emailExists)
        {
            return ApiErrors.BadRequest("Email already exists.");
        }

        user.DisplayName = request.DisplayName.Trim();
        user.Email = request.Email.Trim();
        user.NormalisedEmail = normalisedEmail;
        await scope.SaveChangesAsync();

        return new OwnUserProfileDto(
            user.Id,
            user.UserName,
            user.DisplayName,
            user.Email,
            user.Role.ToString());
    }

    private static IReadOnlyList<ValidationError> ValidateDisplayNameAndEmail(string displayName, string email)
    {
        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            errors.Add(new ValidationError("displayName", "Display name is required."));
        }
        else if (displayName.Trim().Length is < 1 or > 64)
        {
            errors.Add(new ValidationError("displayName", "Display name must be between 1 and 64 characters."));
        }

        errors.AddRange(EmailAddressRules.Validate(email, "email"));
        return errors;
    }
}
