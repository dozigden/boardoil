using BoardOil.Api.Auth;
using BoardOil.Api.Configuration;
using BoardOil.Api.Endpoints;
using BoardOil.Api.Extensions;
using BoardOil.Api.Mcp;
using BoardOil.Api.Realtime;
using BoardOil.Api.Swagger;
using BoardOil.Abstractions;
using BoardOil.Abstractions.Auth;
using BoardOil.Contracts.Contracts;
using BoardOil.Services.DependencyInjection;
using BoardOil.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var runtimeOptions = BoardOilRuntimeOptions.FromConfiguration(builder.Configuration);
var jwtOptions = JwtAuthOptions.FromConfiguration(builder.Configuration);
var csrfOptions = CsrfOptions.FromConfiguration(builder.Configuration);
var internalOptions = BoardOilInternalOptions.FromConfiguration(builder.Configuration);
var buildInfo = BoardOilBuildInfo.FromConfiguration(builder.Configuration, builder.Environment, typeof(Program).Assembly);

builder.WebHost.UseUrls(runtimeOptions.ResolveListenUrl(builder.Configuration));

var connectionString = runtimeOptions.ResolveConnectionString(builder.Configuration);
builder.Services.AddBoardOilServices(connectionString);
builder.Services.AddCors(options =>
{
    options.AddPolicy("BoardOilDevClient", policy =>
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                var isHttp = uri.Scheme is "http" or "https";
                var isLoopbackHost = uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                    || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
                return isHttp && isLoopbackHost;
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});
builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(runtimeOptions);
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton(csrfOptions);
builder.Services.AddSingleton(internalOptions);
builder.Services.AddSingleton(buildInfo);
builder.Services.AddBoardOilMcp();
builder.Services.AddSingleton(new AuthSessionOptions
{
    AccessTokenMinutes = jwtOptions.AccessTokenMinutes,
    RefreshTokenDays = jwtOptions.RefreshTokenDays
});
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
builder.Services.AddScoped<IAuthHttpSessionService, AuthHttpSessionService>();
builder.Services.AddScoped<IConfigurationService, ConfigurationService>();
builder.Services.AddSingleton<IBoardEvents, BoardRealtimeNotifier>();
builder.Services.AddSingleton<IAuthorizationHandler, RequirePatApiScopeHandler>();
builder.Services
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
builder.Services.AddAuthorization(options =>
{
    var patApiScopeRequirement = new RequirePatApiScopeRequirement();

    options.AddPolicy(BoardOilPolicies.AuthenticatedUser, policy =>
        policy
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, McpAuthenticationSchemes.PatBearer)
            .RequireAuthenticatedUser()
            .AddRequirements(patApiScopeRequirement));
    options.AddPolicy(BoardOilPolicies.McpAuthenticated, policy =>
        policy
            .AddAuthenticationSchemes(McpAuthenticationSchemes.PatBearer)
            .RequireAuthenticatedUser());
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
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BoardOil API",
        Version = buildInfo.Version
    });
    options.DocInclusionPredicate((_, apiDescription) =>
    {
        var relativePath = apiDescription.RelativePath ?? string.Empty;
        var path = "/" + relativePath.Split('?', 2)[0].TrimStart('/');
        return path.StartsWith("/api", StringComparison.OrdinalIgnoreCase);
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT bearer token from a user session. Format: `Bearer {token}`."
    });
    options.AddSecurityDefinition("PatBearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "Authorization",
        Description = "Personal access token. Format: `Bearer bo_pat_...`."
    });
    options.OperationFilter<PatSecurityOperationFilter>();
});
var app = builder.Build();
app.InitialiseMcpServiceProvider();

if (jwtOptions.AllowInsecureCookies)
{
    app.Logger.LogWarning("Auth cookies are configured with Secure=false. This should only be used for local/home-lab HTTP setups.");
}

await app.Services.InitializeBoardOilAsync();
app.UseCors("BoardOilDevClient");
app.UseAuthentication();
app.MapBoardOilMcp();
app.Use(async (context, next) =>
{
    if (!HttpMethods.IsPost(context.Request.Method)
        && !HttpMethods.IsPut(context.Request.Method)
        && !HttpMethods.IsPatch(context.Request.Method)
        && !HttpMethods.IsDelete(context.Request.Method))
    {
        await next();
        return;
    }

    if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    if (IsCsrfExemptAuthPath(context.Request.Path))
    {
        await next();
        return;
    }

    if (context.User.Identity?.IsAuthenticated != true)
    {
        await next();
        return;
    }

    if (IsPatAuthenticatedPrincipal(context.User))
    {
        await next();
        return;
    }

    var hasCookie = context.Request.Cookies.TryGetValue(csrfOptions.CookieName, out var csrfCookie);
    var hasHeader = context.Request.Headers.TryGetValue(csrfOptions.HeaderName, out var csrfHeader);
    if (!hasCookie
        || !hasHeader
        || string.IsNullOrWhiteSpace(csrfCookie)
        || string.IsNullOrWhiteSpace(csrfHeader)
        || !string.Equals(csrfCookie, csrfHeader.ToString(), StringComparison.Ordinal))
    {
        var payload = new ApiResult(false, 403, "CSRF validation failed.");
        context.Response.StatusCode = 403;
        await context.Response.WriteAsJsonAsync(payload);
        return;
    }

    await next();
});
app.UseAuthorization();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", $"BoardOil API {buildInfo.Version}");
});
app.MapGet("/swagger.json", () => Results.Redirect("/swagger/v1/swagger.json"));

app.MapHealthEndpoints();
app.MapVersionEndpoints();
app.MapBoardEndpoints();
app.MapSystemBoardEndpoints();
app.MapColumnEndpoints();
app.MapCardEndpoints();
app.MapCardTypeEndpoints();
app.MapTagEndpoints();
app.MapInternalRealtimeEndpoints();
app.MapConfigurationEndpoints();
app.MapUserEndpoints();
app.MapClientAccountEndpoints();

app.MapAuthEndpoints();

app.MapHub<BoardHub>("/hubs/board")
    .RequireAuthorization(BoardOilPolicies.AuthenticatedUser);

app.UseDefaultFiles();
app.UseStaticFiles();

// Frontend SPA fallback once frontend build output is copied into wwwroot.
app.MapFallbackToFile("index.html");

app.Run();

static bool IsCsrfExemptAuthPath(PathString path) =>
    path.StartsWithSegments("/api/auth/register-initial-admin", StringComparison.OrdinalIgnoreCase)
    || path.StartsWithSegments("/api/auth/login", StringComparison.OrdinalIgnoreCase)
    || path.StartsWithSegments("/api/auth/refresh", StringComparison.OrdinalIgnoreCase)
    || path.StartsWithSegments("/api/auth/logout", StringComparison.OrdinalIgnoreCase)
    || path.StartsWithSegments("/api/auth/machine/login", StringComparison.OrdinalIgnoreCase)
    || path.StartsWithSegments("/api/auth/machine/refresh", StringComparison.OrdinalIgnoreCase)
    || path.StartsWithSegments("/api/auth/machine/logout", StringComparison.OrdinalIgnoreCase);

static bool IsPatAuthenticatedPrincipal(ClaimsPrincipal claimsPrincipal)
{
    var authType = claimsPrincipal.FindFirst("boardoil_auth_type")?.Value;
    return string.Equals(authType, "pat", StringComparison.Ordinal);
}

public partial class Program;
