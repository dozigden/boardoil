using System.Threading.RateLimiting;
using BoardOil.Contracts.Auth;
using BoardOil.Ef;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace BoardOil.Api.OAuth;

public static class OAuthServiceCollectionExtensions
{
    public const string DynamicClientRegistrationRateLimitPolicy = "oauth-dynamic-client-registration";

    public static IServiceCollection AddBoardOilOAuth(this IServiceCollection services)
    {
        var options = new BoardOilOAuthOptions();
        services.AddSingleton(options);
        services.AddScoped<OAuthEndpointUrlResolver>();
        services.AddScoped<OpenIddictConfigurationHandler>();
        services.AddScoped<IOAuthProtectedResourceMetadataService, OAuthProtectedResourceMetadataService>();
        services.AddScoped<IOAuthDynamicClientRegistrationService, OAuthDynamicClientRegistrationService>();
        services.AddHostedService<OAuthDynamicClientRegistrationCleanupService>();

        services.AddOpenIddict()
            .AddCore(openIddict =>
            {
                openIddict.UseEntityFrameworkCore()
                    .UseDbContext<BoardOilDbContext>();
            })
            .AddServer(openIddict =>
            {
                openIddict.SetConfigurationEndpointUris(
                        "/.well-known/openid-configuration",
                        "/.well-known/oauth-authorization-server")
                    .SetAuthorizationEndpointUris("/connect/authorize")
                    .SetTokenEndpointUris("/connect/token")
                    .AllowAuthorizationCodeFlow()
                    .AllowRefreshTokenFlow()
                    .RequireProofKeyForCodeExchange()
                    .Configure(options =>
                    {
                        options.CodeChallengeMethods.Clear();
                        options.CodeChallengeMethods.Add(OpenIddictConstants.CodeChallengeMethods.Sha256);
                    })
                    .RegisterScopes(MachinePatScopes.McpRead, MachinePatScopes.McpWrite)
                    .AddEphemeralEncryptionKey()
                    .AddEphemeralSigningKey();

                openIddict.AddEventHandler<OpenIddictServerEvents.HandleConfigurationRequestContext>(handler =>
                    handler.UseScopedHandler<OpenIddictConfigurationHandler>()
                        .SetOrder(OpenIddictServerHandlers.Discovery.AttachAdditionalMetadata.Descriptor.Order + 500));

                openIddict.UseAspNetCore();
            });

        services.AddRateLimiter(rateLimiting =>
        {
            rateLimiting.AddPolicy(DynamicClientRegistrationRateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.DynamicClientRegistrationLimitPerMinute,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1),
                        AutoReplenishment = true,
                    }));
            rateLimiting.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        return services;
    }
}
