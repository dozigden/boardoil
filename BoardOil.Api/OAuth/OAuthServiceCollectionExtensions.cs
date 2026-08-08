using System.Threading.RateLimiting;
using System.Security.Cryptography;
using System.Text;
using BoardOil.Api.Configuration;
using BoardOil.Abstractions.OAuth;
using BoardOil.Contracts.Auth;
using BoardOil.Ef;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Validation;
using OpenIddict.Validation.AspNetCore;

namespace BoardOil.Api.OAuth;

public static class OAuthServiceCollectionExtensions
{
    public const string DynamicClientRegistrationRateLimitPolicy = "oauth-dynamic-client-registration";

    public static IServiceCollection AddBoardOilOAuth(
        this IServiceCollection services,
        JwtAuthOptions jwtOptions)
    {
        var options = new BoardOilOAuthOptions();
        services.AddSingleton(options);
        services.AddScoped<OAuthEndpointUrlResolver>();
        services.AddScoped<OpenIddictPublicBaseUriHandler>();
        services.AddScoped<OpenIddictValidationPublicBaseUriHandler>();
        services.AddScoped<OpenIddictConfigurationHandler>();
        services.AddScoped<OAuthAuthorizationService>();
        services.AddScoped<IOAuthAuthorizationRevoker, OpenIddictOAuthAuthorizationRevoker>();
        services.AddScoped<OAuthRefreshTokenGenerationHandler>();
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
                    .DisableResourceValidation()
                    .IgnoreResourcePermissions()
                    .SetAuthorizationCodeLifetime(options.AuthorizationCodeLifetime)
                    .SetAccessTokenLifetime(options.AccessTokenLifetime)
                    .SetRefreshTokenLifetime(options.RefreshTokenLifetime)
                    .SetRefreshTokenReuseLeeway(options.RefreshTokenReuseLeeway)
                    .Configure(options =>
                    {
                        options.CodeChallengeMethods.Clear();
                        options.CodeChallengeMethods.Add(OpenIddictConstants.CodeChallengeMethods.Sha256);
                    })
                    .RegisterScopes(MachinePatScopes.McpRead, MachinePatScopes.McpWrite)
                    .AddEncryptionKey(CreateKey(jwtOptions.SigningKey, "boardoil-oauth-encryption"))
                    .AddSigningKey(CreateKey(jwtOptions.SigningKey, "boardoil-oauth-signing"))
                    .AddEphemeralSigningKey();

                openIddict.AddEventHandler<OpenIddictServerEvents.HandleConfigurationRequestContext>(handler =>
                    handler.UseScopedHandler<OpenIddictConfigurationHandler>()
                        .SetOrder(OpenIddictServerHandlers.Discovery.AttachAdditionalMetadata.Descriptor.Order + 500));
                openIddict.AddEventHandler<OpenIddictServerEvents.ProcessRequestContext>(handler =>
                    handler.UseScopedHandler<OpenIddictPublicBaseUriHandler>()
                        .SetOrder(OpenIddictServerHandlers.InferEndpointType.Descriptor.Order + 500));
                openIddict.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(handler =>
                    handler.UseScopedHandler<OAuthRefreshTokenGenerationHandler>()
                        .SetOrder(OpenIddictServerHandlers.EvaluateGeneratedTokens.Descriptor.Order + 500));

                openIddict.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough();
            })
            .AddValidation(openIddict =>
            {
                openIddict.UseLocalServer();
                openIddict.EnableAuthorizationEntryValidation();
                openIddict.EnableTokenEntryValidation();
                openIddict.AddEventHandler<OpenIddictValidationEvents.ProcessRequestContext>(handler =>
                    handler.UseScopedHandler<OpenIddictValidationPublicBaseUriHandler>()
                        .SetOrder(OpenIddictValidationAspNetCoreHandlers.ResolveRequestUri.Descriptor.Order + 500));
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

    private static SymmetricSecurityKey CreateKey(string secret, string purpose)
    {
        var material = Encoding.UTF8.GetBytes($"{purpose}\0{secret}");
        return new SymmetricSecurityKey(SHA256.HashData(material));
    }
}
