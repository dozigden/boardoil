using BoardOil.Api.Configuration;
using BoardOil.Api.Extensions;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Api.Endpoints;

public static class VersionEndpoints
{
    public static IEndpointRouteBuilder MapVersionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/version", (BoardOilBuildInfo buildInfo) =>
            ApiResults.Ok(buildInfo).ToHttpResult())
            .WithTags("Other");
        return app;
    }
}
