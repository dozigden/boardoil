using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Services.Auth;

public static class BoardOilRoles
{
    public const string Admin = nameof(UserRole.Admin);
    public const string Standard = nameof(UserRole.Standard);
}
