namespace BoardOil.Contracts.Users;

public sealed record ManagedUserDto(
    int Id,
    string UserName,
    string Role,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CreateUserRequest(string UserName, string Password, string Role);

public sealed record UpdateUserRoleRequest(string Role);

public sealed record UpdateUserStatusRequest(bool IsActive);
