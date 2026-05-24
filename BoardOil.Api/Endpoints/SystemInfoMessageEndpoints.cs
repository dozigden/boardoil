using BoardOil.Api.Extensions;
using BoardOil.Abstractions.Configuration;
using BoardOil.Contracts.Configuration;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class SystemInfoMessageEndpoints
{
    public static IEndpointRouteBuilder MapSystemInfoMessageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/system/system-info-message", async (ISystemInfoMessageService systemInfoMessageService) =>
                (await systemInfoMessageService.GetAsync()).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .WithTags("SystemInfoMessage");

        app.MapPut("/api/system/system-info-message", async (SystemInfoMessageDto? request, ISystemInfoMessageService systemInfoMessageService) =>
                (await systemInfoMessageService.UpdateAsync(request)).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("SystemInfoMessage");

        return app;
    }
}
