using BoardOil.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BoardOil.Api.Realtime;

[Authorize(Policy = BoardOilPolicies.AdminOnly)]
public sealed class SystemJobsHub : Hub;
