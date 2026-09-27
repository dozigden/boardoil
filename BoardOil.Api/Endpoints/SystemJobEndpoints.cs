using BoardOil.Abstractions.Jobs;
using BoardOil.Api.Extensions;
using BoardOil.Contracts.Jobs;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class SystemJobEndpoints
{
    public static IEndpointRouteBuilder MapSystemJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/system/jobs")
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("System Jobs");

        group.MapGet(string.Empty, async (int? offset, int? limit, IJobService service, CancellationToken token) =>
            (await service.ListAsync(new JobListRequest(offset ?? 0, limit ?? 100), token)).ToHttpResult());
        group.MapGet("/{id:int}", async (int id, IJobService service, CancellationToken token) =>
            (await service.GetAsync(id, token)).ToHttpResult());
        return app;
    }
}
