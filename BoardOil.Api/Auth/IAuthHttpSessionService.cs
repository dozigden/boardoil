using System.Security.Claims;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Auth;

public interface IAuthHttpSessionService
{
    Task<IResult> RegisterInitialAdminAsync(RegisterInitialAdminRequest request, HttpResponse response);
    Task<IResult> LoginAsync(LoginRequest request, HttpResponse response);
    Task<IResult> RefreshAsync(HttpRequest request, HttpResponse response);
    Task<IResult> LogoutAsync(HttpRequest request, HttpResponse response);
    IResult GetCsrf(HttpRequest request, HttpResponse response);
    Task<IResult> GetMeAsync(ClaimsPrincipal claimsPrincipal);
}
