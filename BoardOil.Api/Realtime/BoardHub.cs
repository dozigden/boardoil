using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Realtime;

[Authorize(Policy = BoardOilPolicies.AuthenticatedUser)]
public sealed class BoardHub : Hub
{
}
