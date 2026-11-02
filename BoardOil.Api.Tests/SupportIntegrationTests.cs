using System.Net;
using System.Net.Http.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.Support;
using BoardOil.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class SupportIntegrationTests : TestBaseIntegration
{
    [Fact]
    public async Task SupportInfo_ShouldReturnCurrentMetadataUnderAdminPolicy()
    {
        var response = await Client.GetAsync("/api/system/support/info", TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<SupportInfoDto>>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(result!.Success);
        Assert.Equal("SQLite", result.Data!.Database.Provider);
        Assert.Equal("wal", result.Data.Database.JournalMode);
        Assert.NotEmpty(result.Data.Database.Version);
        Assert.True(result.Data.Database.SizeBytes > 0);
        var endpoint = Factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == "/api/system/support/info");
        Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            auth => auth.Policy == BoardOilPolicies.AdminOnly);
    }
}
