using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Users;
using BoardOil.Persistence.Abstractions.Auth;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Persistence.Abstractions.Users;
using BoardOil.Contracts.Contracts;
using BoardOil.Contracts.Users;
using BoardOil.Services.Auth;
using BoardOil.Abstractions.Auth;

namespace BoardOil.Services.Users;

public sealed class UserAdminService(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHashService passwordHashService,
    TimeProvider timeProvider,
    IDbContextScopeFactory scopeFactory) : IUserAdminService
{
    public async Task<ApiResult<IReadOnlyList<ManagedUserDto>>> GetUsersAsync()
    {
        using var scope = scopeFactory.CreateReadOnly();

        var users = (await userRepository.GetUsersOrderedAsync())
            .Where(x => x.IdentityType == UserIdentityType.User)
            .Select(x => x.ToManagedUserDto())
            .ToList();

        return users;
    }

    public async Task<ApiResult<ManagedUserDto>> CreateUserAsync(CreateUserRequest request)
    {
        using var scope = scopeFactory.Create();

        var validation = ValidateCredentials(request.UserName, request.Email, request.Password);
        if (validation.Count > 0)
        {
            return ApiErrors.BadRequest("Validation failed.", validation);
        }

        if (!TryParseRole(request.Role, out var role))
        {
            return ApiErrors.BadRequest("Role must be 'Admin' or 'Standard'.");
        }

        var userName = request.UserName.Trim();
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
            Email = email,
            NormalisedEmail = normalisedEmail,
            PasswordHash = passwordHashService.HashPassword(request.Password),
            Role = role,
            IdentityType = UserIdentityType.User,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        userRepository.Add(user);
        await scope.SaveChangesAsync();

        return user.ToManagedUserDto();
    }

    public async Task<ApiResult<ManagedUserDto>> UpdateUserAsync(int id, UpdateUserRequest request)
    {
        using var scope = scopeFactory.Create();

        if (!TryParseRole(request.Role, out var role))
        {
            return ApiErrors.BadRequest("Role must be 'Admin' or 'Standard'.");
        }

        var emailValidation = EmailAddressRules.Validate(request.Email, "email");
        if (emailValidation.Count > 0)
        {
            return ApiErrors.BadRequest("Validation failed.", emailValidation);
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

        user.Email = request.Email.Trim();
        user.NormalisedEmail = normalisedEmail;
        user.Role = role;
        user.IsActive = request.IsActive;
        user.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await scope.SaveChangesAsync();

        return user.ToManagedUserDto();
    }

    public async Task<ApiResult<ManagedUserDto>> UpdateUserRoleAsync(int id, UpdateUserRoleRequest request)
    {
        using var scope = scopeFactory.Create();

        if (!TryParseRole(request.Role, out var role))
        {
            return ApiErrors.BadRequest("Role must be 'Admin' or 'Standard'.");
        }

        var user = userRepository.Get(id);
        if (user is null || user.IdentityType != UserIdentityType.User)
        {
            return ApiErrors.NotFound("User not found.");
        }

        var adminGuardError = await ValidateAdminUpdateAsync(user, role, user.IsActive);
        if (adminGuardError is not null)
        {
            return adminGuardError;
        }

        user.Role = role;
        user.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await scope.SaveChangesAsync();

        return user.ToManagedUserDto();
    }

    public async Task<ApiResult<ManagedUserDto>> UpdateUserStatusAsync(int id, UpdateUserStatusRequest request)
    {
        using var scope = scopeFactory.Create();

        var user = userRepository.Get(id);
        if (user is null || user.IdentityType != UserIdentityType.User)
        {
            return ApiErrors.NotFound("User not found.");
        }

        var adminGuardError = await ValidateAdminUpdateAsync(user, user.Role, request.IsActive);
        if (adminGuardError is not null)
        {
            return adminGuardError;
        }

        user.IsActive = request.IsActive;
        user.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await scope.SaveChangesAsync();

        return user.ToManagedUserDto();
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
            return ApiErrors.BadRequest("Validation failed.", passwordValidation);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        user.PasswordHash = passwordHashService.HashPassword(request.NewPassword);
        user.UpdatedAtUtc = now;
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

        userRepository.Remove(user);
        await scope.SaveChangesAsync();

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
