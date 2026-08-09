using BoardOil.Abstractions.ErrorLogs;
using BoardOil.Api.Auth;
using BoardOil.Api.ErrorLogs;
using BoardOil.Api.Extensions;
using BoardOil.Contracts.ErrorLogs;
using BoardOil.Services.Auth;
using Microsoft.AspNetCore.Mvc;

namespace BoardOil.Api.Endpoints;

public static class ErrorLogEndpoints
{
    private const long ClientErrorReportRequestMaxLength = 64 * 1024;

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

        app.MapPost(
                "/api/system/error-logs:report-client-error",
                async (
                    ClientErrorReportRequest request,
                    HttpContext httpContext,
                    IErrorLogService errorLogService,
                    CancellationToken cancellationToken) =>
                    (await errorLogService.ReportClientErrorAsync(
                        request,
                        httpContext.GetActorUserId(),
                        cancellationToken)).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .AddEndpointFilter<RequireActorUserIdFilter>()
            .RequireRateLimiting(ErrorLogRateLimitExtensions.ClientErrorReportPolicy)
            .WithMetadata(new RequestSizeLimitAttribute(ClientErrorReportRequestMaxLength))
            .WithTags("ErrorLogs");

        return app;
    }
}
