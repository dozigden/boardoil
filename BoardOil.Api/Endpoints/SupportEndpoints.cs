using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.Extensions;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.Support;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class SupportEndpoints
{
    public static IEndpointRouteBuilder MapSupportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/system/support/info", async (
                IDatabaseInfoProvider databaseInfo, CancellationToken cancellationToken) =>
                ApiResults.Ok(new SupportInfoDto(await databaseInfo.GetAsync(cancellationToken))).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("Support");
        return app;
    }
}
