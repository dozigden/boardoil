using BoardOil.Abstractions.Configuration;
using BoardOil.Abstractions.Jobs;
using BoardOil.Api.Auth;
using BoardOil.Api.Extensions;
using BoardOil.Contracts.Configuration;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class ScheduledJobEndpoints
{
    public static IEndpointRouteBuilder MapScheduledJobEndpoints(this IEndpointRouteBuilder app)
    {
        var schedules = app.MapGroup("/api/system/scheduled-jobs")
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .AddEndpointFilter<RequireActorUserIdFilter>()
            .WithTags("Scheduled Jobs");
        schedules.MapGet(string.Empty, async (IScheduledJobService service, CancellationToken token) =>
            (await service.ListAsync(token)).ToHttpResult());
        schedules.MapPost("/{name}/runs", async (
            string name, HttpContext context, IScheduledJobService service, CancellationToken token) =>
            (await service.RunNowAsync(name, context.GetActorUserId(), token)).ToHttpResult());

        var timezone = app.MapGroup("/api/system/timezone")
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System Timezone");
        timezone.MapGet(string.Empty, async (ISystemTimeZoneService service, CancellationToken token) =>
            (await service.GetAsync(token)).ToHttpResult());
        timezone.MapGet("/options", (ISystemTimeZoneService service) => service.GetOptions().ToHttpResult());
        timezone.MapPut(string.Empty, async (
            UpdateSystemTimeZoneRequest request, ISystemTimeZoneService service, CancellationToken token) =>
            (await service.UpdateAsync(request, token)).ToHttpResult());
        return app;
    }
}
