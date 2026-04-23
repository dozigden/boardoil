using BoardOil.Contracts.Auth;

namespace BoardOil.Contracts.Users;

public sealed record ManagedUserDto(
    int Id,
    string UserName,
    string Email,
    string Role,
    string IdentityType,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record UserDirectoryEntryDto(
    int Id,
    string UserName,
    bool IsActive);

public sealed record CreateUserRequest(string UserName, string Email, string Password, string Role);

public sealed record ClientAccountDto(
    int Id,
    string UserName,
    string Email,
    string Role,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CreateClientAccountRequest(
    string UserName,
    string Email,
    string Role,
    string? TokenName = null,
    int? ExpiresInDays = null,
    string[]? Scopes = null);

public sealed record CreateClientAccessTokenRequest(
    string Name,
    int? ExpiresInDays = null,
    string[]? Scopes = null);

public sealed record CreatedClientAccountDto(
    ClientAccountDto Account,
    CreatedMachinePatDto Token);

public sealed record UpdateUserRequest(string Email, string Role, bool IsActive);
public sealed record UpdateClientAccountRequest(string Email, string Role, bool IsActive);

public sealed record UpdateUserRoleRequest(string Role);

public sealed record UpdateUserStatusRequest(bool IsActive);
public sealed record ResetUserPasswordRequest(string NewPassword);
