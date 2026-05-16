using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Image;
using BoardOil.Abstractions.Users;
using BoardOil.Data.Abstractions.Auth;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Users;
using BoardOil.Contracts.Contracts;
using BoardOil.Contracts.Users;
using BoardOil.Services.Auth;
using BoardOil.Abstractions.Auth;
using BoardOil.Data.Abstractions.Image;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Services.Users;

public sealed class UserAdminService(
    IUserRepository userRepository,
    IImageRepository imageRepository,
    IImageStorageService imageStorageService,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHashService passwordHashService,
    TimeProvider timeProvider,
    IDbContextScopeFactory scopeFactory) : IUserAdminService
{
    public async Task<ApiResult<IReadOnlyList<ManagedUserDto>>> GetUsersAsync()
    {
        using var scope = scopeFactory.CreateReadOnly();

        var entities = (await userRepository.GetUsersOrderedAsync())
            .Where(x => x.IdentityType == UserIdentityType.User)
            .ToList();

        var userIds = entities.Select(x => x.Id).ToArray();
        var latestImages = await imageRepository.GetLatestForEntitiesAsync(ImageEntityType.UserProfile, userIds);
        var imagePathByUserId = latestImages.ToDictionary(x => x.EntityId, x => x.RelativePath);

        var users = entities
            .Select(x => x.ToManagedUserDto(imagePathByUserId.GetValueOrDefault(x.Id)))
            .ToList();

        return users;
    }

    public async Task<ApiResult<ManagedUserDto>> CreateUserAsync(CreateUserRequest request)
    {
        using var scope = scopeFactory.Create();

        var validation = ValidateCredentials(request.UserName, request.DisplayName, request.Email, request.Password);
        if (validation.Count > 0)
        {
            return ApiErrors.ValidationFailed(validation);
        }

        if (!TryParseRole(request.Role, out var role))
        {
            return ApiErrors.BadRequest("Role must be 'Admin' or 'Standard'.");
        }

        var userName = request.UserName.Trim();
        var displayName = request.DisplayName.Trim();
        var email = request.Email.Trim();
        var normalisedEmail = EmailAddressRules.TryNormalise(email)!;
        var exists = await userRepository.UserNameExistsAsync(userName);
        if (exists)
        {
            return ApiErrors.BadRequest("Username already exists.");
        }

        var emailExists = await userRepository.NormalisedEmailExistsAsync(normalisedEmail);
        if (emailExists)
        {
            return ApiErrors.BadRequest("Email already exists.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var user = new EntityUser
        {
            UserName = userName,
            DisplayName = displayName,
            Email = email,
            NormalisedEmail = normalisedEmail,
            PasswordHash = passwordHashService.HashPassword(request.Password),
            Role = role,
            IdentityType = UserIdentityType.User,
            IsActive = true,
        };

        userRepository.Add(user);
        await scope.SaveChangesAsync();

        return user.ToManagedUserDto(await ResolveProfileImageRelativePathAsync(user.Id));
    }

    public async Task<ApiResult<ManagedUserDto>> UpdateUserAsync(int id, UpdateUserRequest request)
    {
        using var scope = scopeFactory.Create();

        if (!TryParseRole(request.Role, out var role))
        {
            return ApiErrors.BadRequest("Role must be 'Admin' or 'Standard'.");
        }

        var validation = ValidateDisplayNameAndEmail(request.DisplayName, request.Email);
        if (validation.Count > 0)
        {
            return ApiErrors.ValidationFailed(validation);
        }

        var user = userRepository.Get(id);
        if (user is null || user.IdentityType != UserIdentityType.User)
        {
            return ApiErrors.NotFound("User not found.");
        }

        var normalisedEmail = EmailAddressRules.TryNormalise(request.Email)!;
        var emailExists = await userRepository.NormalisedEmailExistsForOtherUserAsync(user.Id, normalisedEmail);
        if (emailExists)
        {
            return ApiErrors.BadRequest("Email already exists.");
        }

        var adminGuardError = await ValidateAdminUpdateAsync(user, role, request.IsActive);
        if (adminGuardError is not null)
        {
            return adminGuardError;
        }

        user.DisplayName = request.DisplayName.Trim();
        user.Email = request.Email.Trim();
        user.NormalisedEmail = normalisedEmail;
        user.Role = role;
        user.IsActive = request.IsActive;
        await scope.SaveChangesAsync();

        return user.ToManagedUserDto(await ResolveProfileImageRelativePathAsync(user.Id));
    }

    public async Task<ApiResult> ResetUserPasswordAsync(int id, ResetUserPasswordRequest request)
    {
        using var scope = scopeFactory.Create();

        var user = userRepository.Get(id);
        if (user is null || user.IdentityType != UserIdentityType.User)
        {
            return ApiErrors.NotFound("User not found.");
        }

        var passwordValidation = ValidatePassword(request.NewPassword, "newPassword");
        if (passwordValidation.Count > 0)
        {
            return ApiErrors.ValidationFailed(passwordValidation);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        user.PasswordHash = passwordHashService.HashPassword(request.NewPassword);
        await refreshTokenRepository.RevokeActiveTokensByUserIdAsync(user.Id, now);
        await scope.SaveChangesAsync();

        return ApiResults.Ok();
    }

    public async Task<ApiResult> DeleteUserAsync(int id, int actorUserId)
    {
        using var scope = scopeFactory.Create();

        if (id == actorUserId)
        {
            return ApiErrors.BadRequest("Cannot delete your own account.");
        }

        var user = userRepository.Get(id);
        if (user is null || user.IdentityType != UserIdentityType.User)
        {
            return ApiErrors.NotFound("User not found.");
        }

        if (user.Role == UserRole.Admin && user.IsActive)
        {
            var activeAdminCount = await userRepository.CountActiveAdminsAsync();
            if (activeAdminCount <= 1)
            {
                return ApiErrors.BadRequest("Cannot delete the last active admin.");
            }
        }

        var profileImages = await imageRepository.Query()
            .Where(x => x.EntityType == ImageEntityType.UserProfile && x.EntityId == id)
            .ToListAsync();
        if (profileImages.Count > 0)
        {
            imageRepository.RemoveRange(profileImages);
        }

        var imagePathsToDelete = profileImages
            .Select(x => x.RelativePath)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        userRepository.Remove(user);
        await scope.SaveChangesAsync();

        foreach (var imagePath in imagePathsToDelete)
        {
            await imageStorageService.DeleteIfExistsAsync(imagePath);
        }

        return ApiResults.Ok();
    }

    private async Task<ApiError?> ValidateAdminUpdateAsync(EntityUser user, UserRole nextRole, bool nextIsActive)
    {
        var isDemotingLastAdmin = user.Role == UserRole.Admin && nextRole != UserRole.Admin;
        if (isDemotingLastAdmin)
        {
            var activeAdminCount = await userRepository.CountActiveAdminsAsync();
            if (activeAdminCount <= 1)
            {
                return ApiErrors.BadRequest("Cannot remove the last active admin.");
            }
        }

        var isDeactivatingLastAdmin = user.Role == UserRole.Admin && user.IsActive && !nextIsActive;
        if (isDeactivatingLastAdmin)
        {
            var activeAdminCount = await userRepository.CountActiveAdminsAsync();
            if (activeAdminCount <= 1)
            {
                return ApiErrors.BadRequest("Cannot deactivate the last active admin.");
            }
        }

        return null;
    }

    private static IReadOnlyList<ValidationError> ValidateCredentials(string userName, string email, string password)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(userName))
        {
            errors.Add(new ValidationError("userName", "Username is required."));
        }
        else if (userName.Trim().Length is < 1 or > 64)
        {
            errors.Add(new ValidationError("userName", "Username must be between 1 and 64 characters."));
        }

        errors.AddRange(EmailAddressRules.Validate(email, "email"));
        errors.AddRange(ValidatePassword(password, "password"));

        return errors;
    }

    private static IReadOnlyList<ValidationError> ValidateCredentials(string userName, string displayName, string email, string password)
    {
        var errors = ValidateCredentials(userName, email, password).ToList();
        errors.AddRange(ValidateDisplayName(displayName));
        return errors;
    }

    private static IReadOnlyList<ValidationError> ValidateDisplayNameAndEmail(string displayName, string email)
    {
        var errors = ValidateDisplayName(displayName).ToList();
        errors.AddRange(EmailAddressRules.Validate(email, "email"));
        return errors;
    }

    private static IReadOnlyList<ValidationError> ValidateDisplayName(string displayName)
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

        return errors;
    }

    private async Task<string?> ResolveProfileImageRelativePathAsync(int userId)
    {
        var image = await imageRepository.GetLatestForEntityAsync(ImageEntityType.UserProfile, userId);
        return image?.RelativePath;
    }

    private static IReadOnlyList<ValidationError> ValidatePassword(string password, string fieldName)
    {
        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add(new ValidationError(fieldName, "Password is required."));
        }
        else if (password.Length < 10)
        {
            errors.Add(new ValidationError(fieldName, "Password must be at least 10 characters."));
        }

        return errors;
    }

    private static bool TryParseRole(string roleValue, out UserRole role)
    {
        if (string.Equals(roleValue, BoardOilRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            role = UserRole.Admin;
            return true;
        }

        if (string.Equals(roleValue, BoardOilRoles.Standard, StringComparison.OrdinalIgnoreCase))
        {
            role = UserRole.Standard;
            return true;
        }

        role = default;
        return false;
    }
}
