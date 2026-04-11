using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Users;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Persistence.Abstractions.Users;
using BoardOil.Contracts.Contracts;
using BoardOil.Contracts.Users;
using BoardOil.Services.Auth;
using BoardOil.Abstractions.Auth;

namespace BoardOil.Services.Users;

public sealed class UserAdminService(
    IUserRepository userRepository,
    IPasswordHashService passwordHashService,
    TimeProvider timeProvider,
    IDbContextScopeFactory scopeFactory) : IUserAdminService
{
    public async Task<ApiResult<IReadOnlyList<ManagedUserDto>>> GetUsersAsync()
    {
        using var scope = scopeFactory.CreateReadOnly();

        var users = (await userRepository.GetUsersOrderedAsync())
            .Select(x => x.ToManagedUserDto())
            .ToList();

        return users;
    }

    public async Task<ApiResult<ManagedUserDto>> CreateUserAsync(CreateUserRequest request)
    {
        using var scope = scopeFactory.Create();

        var validation = ValidateCredentials(request.UserName, request.Password);
        if (validation.Count > 0)
        {
            return ApiErrors.BadRequest("Validation failed.", validation);
        }

        if (!TryParseRole(request.Role, out var role))
        {
            return ApiErrors.BadRequest("Role must be 'Admin' or 'Standard'.");
        }

        var userName = request.UserName.Trim();
        var exists = await userRepository.UserNameExistsAsync(userName);
        if (exists)
        {
            return ApiErrors.BadRequest("Username already exists.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var user = new EntityUser
        {
            UserName = userName,
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

    public async Task<ApiResult<ManagedUserDto>> UpdateUserRoleAsync(int id, UpdateUserRoleRequest request)
    {
        using var scope = scopeFactory.Create();

        if (!TryParseRole(request.Role, out var role))
        {
            return ApiErrors.BadRequest("Role must be 'Admin' or 'Standard'.");
        }

        var user = userRepository.Get(id);
        if (user is null)
        {
            return ApiErrors.NotFound("User not found.");
        }

        if (user.Role == UserRole.Admin && role != UserRole.Admin)
        {
            var activeAdminCount = await userRepository.CountActiveAdminsAsync();
            if (activeAdminCount <= 1)
            {
                return ApiErrors.BadRequest("Cannot remove the last active admin.");
            }
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
        if (user is null)
        {
            return ApiErrors.NotFound("User not found.");
        }

        if (!request.IsActive && user.IsActive && user.Role == UserRole.Admin)
        {
            var activeAdminCount = await userRepository.CountActiveAdminsAsync();
            if (activeAdminCount <= 1)
            {
                return ApiErrors.BadRequest("Cannot deactivate the last active admin.");
            }
        }

        user.IsActive = request.IsActive;
        user.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await scope.SaveChangesAsync();

        return user.ToManagedUserDto();
    }

    private static IReadOnlyList<ValidationError> ValidateCredentials(string userName, string password)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(userName))
        {
            errors.Add(new ValidationError("userName", "Username is required."));
        }
        else if (userName.Trim().Length is < 3 or > 64)
        {
            errors.Add(new ValidationError("userName", "Username must be between 3 and 64 characters."));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add(new ValidationError("password", "Password is required."));
        }
        else if (password.Length < 10)
        {
            errors.Add(new ValidationError("password", "Password must be at least 10 characters."));
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
