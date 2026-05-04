using System.Net.Http;
using BoardOil.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class AuthPolicyEndpointMappingTests : ApiFactoryIntegrationTestBase
{
    private static readonly HashSet<RouteKey> AnonymousApiRoutes =
    [
        new("POST", "/api/auth/register-initial-admin"),
        new("POST", "/api/auth/login"),
        new("POST", "/api/auth/refresh"),
        new("POST", "/api/auth/logout"),
        new("POST", "/api/auth/machine/login"),
        new("POST", "/api/auth/machine/refresh"),
        new("POST", "/api/auth/machine/logout"),
        new("GET", "/api/auth/bootstrap-status"),
        new("GET", "/api/health"),
        new("GET", "/api/version"),
        new("POST", "/api/internal/realtime/board-events")
    ];

    [Fact]
    public void ApiRoutes_ShouldRequireAuthorization_WhenNotOnAnonymousAllowList()
    {
        using var scope = Factory.Services.CreateScope();
        var endpointSources = scope.ServiceProvider.GetServices<EndpointDataSource>();
        var routeEndpoints = endpointSources
            .SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        var failures = new List<string>();
        foreach (var endpoint in routeEndpoints)
        {
            var route = endpoint.RoutePattern.RawText;
            if (string.IsNullOrWhiteSpace(route) || !route.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods
                ?.Select(x => x.ToUpperInvariant())
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            if (methods is null || methods.Length == 0)
            {
                continue;
            }

            var key = new RouteKey(string.Join('|', methods), route);
            var hasAuthorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0;
            if (!AnonymousApiRoutes.Contains(key) && !hasAuthorize)
            {
                failures.Add($"{string.Join('|', methods)} {route}");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"Found API route(s) missing authorization metadata: {string.Join(", ", failures.OrderBy(x => x, StringComparer.Ordinal))}");
    }

    private readonly record struct RouteKey(string Methods, string Route);
}
