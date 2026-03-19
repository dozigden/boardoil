using BoardOil.Api.Extensions;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // API health endpoint used for container/dev smoke checks.
        app.MapGet("/api/health", () => ApiResults.Ok(new { status = "ok" }).ToHttpResult());
        return app;
    }
}
