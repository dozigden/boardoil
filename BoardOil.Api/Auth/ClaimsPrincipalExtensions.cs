using System.Security.Claims;

namespace BoardOil.Api.Auth;

internal static class ClaimsPrincipalExtensions
{
    public static bool TryGetUserId(this ClaimsPrincipal claimsPrincipal, out int userId)
    {
        var userIdClaim = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out userId);
    }
}
