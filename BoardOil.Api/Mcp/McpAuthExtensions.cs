using BoardOil.Api.Auth;
using BoardOil.Api.Configuration;
using BoardOil.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Validation.AspNetCore;
using System.Text;

namespace BoardOil.Api.Mcp;

public static class McpAuthExtensions
{
    public static AuthenticationBuilder AddBoardOilAuthentication(
        this IServiceCollection services,
        JwtAuthOptions jwtOptions)
    {
        return services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, McpPatAuthenticationHandler>(McpAuthenticationSchemes.PatBearer, _ => { })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var authHeader = context.Request.Headers.Authorization.ToString();
                        if (!string.IsNullOrWhiteSpace(authHeader)
                            && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        {
                            var bearerToken = authHeader["Bearer ".Length..].Trim();
                            if (bearerToken.StartsWith("bo_pat_", StringComparison.OrdinalIgnoreCase))
                            {
                                context.NoResult();
                                return Task.CompletedTask;
                            }
                        }

                        if (string.IsNullOrWhiteSpace(context.Token)
                            && context.Request.Cookies.TryGetValue(jwtOptions.AccessTokenCookieName, out var cookieToken))
                        {
                            context.Token = cookieToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnChallenge = async context =>
                    {
                        if (!context.Request.Path.StartsWithSegments("/mcp", StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        context.HandleResponse();
                        var configurationService = context.HttpContext.RequestServices.GetRequiredService<IConfigurationService>();
                        var errorFactory = context.HttpContext.RequestServices.GetRequiredService<IMcpErrorResponseFactory>();
                        var mcpPublicBaseUrl = await configurationService.GetMcpPublicBaseUrlAsync();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.Headers.WWWAuthenticate = "Bearer realm=\"BoardOil MCP\"";
                        await context.Response.WriteAsJsonAsync(errorFactory.CreateAuthError(mcpPublicBaseUrl, "Invalid or expired bearer token."));
                    }
                };
            });
    }

    public static void AddBoardOilAuthorizationPolicies(this AuthorizationOptions options, BoardOilMcpOptions mcpOptions)
    {
        var patApiScopeRequirement = new RequirePatApiScopeRequirement();

        options.AddPolicy(BoardOilPolicies.AuthenticatedUser, policy =>
            policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, McpAuthenticationSchemes.PatBearer)
                .RequireAuthenticatedUser()
                .AddRequirements(patApiScopeRequirement));
        options.AddPolicy(BoardOilPolicies.McpAuthenticated, policy =>
        {
            policy.AddAuthenticationSchemes(McpAuthenticationSchemes.PatBearer);
            if (mcpOptions.AuthMode is McpAuthMode.Pat)
            {
                policy.RequireAuthenticatedUser();
                return;
            }

            policy.RequireAssertion(_ => true);
        });
        options.AddPolicy(BoardOilPolicies.McpOAuthConnection, policy =>
            policy
                .AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .AddRequirements(new McpOAuthConnectionRequirement()));
        options.AddPolicy(BoardOilPolicies.AdminOnly, policy =>
            policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, McpAuthenticationSchemes.PatBearer)
                .RequireRole(BoardOilRoles.Admin)
                .AddRequirements(patApiScopeRequirement));
        options.AddPolicy(BoardOilPolicies.CardEditor, policy =>
            policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, McpAuthenticationSchemes.PatBearer)
                .RequireRole(BoardOilRoles.Admin, BoardOilRoles.Standard)
                .AddRequirements(patApiScopeRequirement));
    }
}
