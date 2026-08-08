using System.Threading.RateLimiting;
using BoardOil.Api.OAuth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BoardOil.Api.Tests.Infrastructure;

internal static class OAuthTestServiceConfiguration
{
    public static void DisableDynamicClientRegistrationRateLimit(
        IServiceCollection services,
        string partitionKey)
    {
        services.RemoveAll<IConfigureOptions<RateLimiterOptions>>();
        services.AddRateLimiter(options =>
            options.AddPolicy(
                OAuthServiceCollectionExtensions.DynamicClientRegistrationRateLimitPolicy,
                _ => RateLimitPartition.GetNoLimiter(partitionKey)));
    }
}
