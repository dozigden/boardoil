using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;

namespace BoardOil.Api.Configuration;

internal static class BoardOilDataProtectionServiceCollectionExtensions
{
    public static void AddBoardOilEphemeralDataProtection(this IServiceCollection services)
    {
        // OAuth consent antiforgery tokens are intentionally scoped to one application lifetime.
        // A restart expires open consent forms instead of making another key ring persistent installation state.
        services.AddDataProtection()
            .UseEphemeralDataProtectionProvider();

        // ASP.NET still registers a hosted service that eagerly creates the unused default XML key ring.
        // Discover and remove its internal type through the framework's own registration rather than
        // depending on an internal type name that can change between framework versions.
        var defaultDataProtectionServices = new ServiceCollection();
        defaultDataProtectionServices.AddDataProtection();
        var dataProtectionHostedServiceTypes = defaultDataProtectionServices
            .Where(service => service.ServiceType == typeof(IHostedService))
            .Select(service => service.ImplementationType)
            .OfType<Type>()
            .ToHashSet();
        var unusedKeyRingHostedServices = services
            .Where(service =>
                service.ServiceType == typeof(IHostedService)
                && service.ImplementationType is not null
                && dataProtectionHostedServiceTypes.Contains(service.ImplementationType))
            .ToArray();
        foreach (var hostedService in unusedKeyRingHostedServices)
        {
            services.Remove(hostedService);
        }
    }
}
