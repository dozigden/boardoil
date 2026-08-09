using BoardOil.Abstractions.ErrorLogs;
using BoardOil.Api.Extensions;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class ErrorLogEndpoints
{
    public static IEndpointRouteBuilder MapErrorLogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/system/error-logs")
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("ErrorLogs");

        group.MapGet(string.Empty, async (
                int? offset,
                int? limit,
                IErrorLogService errorLogService) =>
            (await errorLogService.ListAsync(offset, limit)).ToHttpResult());

        group.MapGet("/{id:int}", async (int id, IErrorLogService errorLogService) =>
            (await errorLogService.GetAsync(id)).ToHttpResult());

        app.MapPost(
                "/api/system/error-logs:purge",
                async (IErrorLogService errorLogService, CancellationToken cancellationToken) =>
                    (await errorLogService.PurgeExpiredAsync(cancellationToken)).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("ErrorLogs");

        return app;
    }
}
