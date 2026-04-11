using BoardOil.Api.Configuration;
using BoardOil.Api.Extensions;
using BoardOil.Contracts.Configuration;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class ConfigurationEndpoints
{
    public static IEndpointRouteBuilder MapConfigurationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/system/configuration", async (IConfigurationService configurationService) =>
                (await configurationService.GetConfigurationAsync()).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("Configuration");

        app.MapPut("/api/system/configuration", async (UpdateConfigurationRequest request, IConfigurationService configurationService) =>
                (await configurationService.UpdateConfigurationAsync(request)).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .WithTags("Configuration");

        return app;
    }
}
