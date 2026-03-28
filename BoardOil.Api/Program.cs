using BoardOil.Api.Auth;
using BoardOil.Api.Configuration;
using BoardOil.Api.Endpoints;
using BoardOil.Api.Extensions;
using BoardOil.Api.Mcp;
using BoardOil.Api.Realtime;
using BoardOil.Abstractions;
using BoardOil.Abstractions.Auth;
using BoardOil.Contracts.Contracts;
using BoardOil.Services.DependencyInjection;
using BoardOil.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var runtimeOptions = BoardOilRuntimeOptions.FromConfiguration(builder.Configuration);
var jwtOptions = JwtAuthOptions.FromConfiguration(builder.Configuration);
var csrfOptions = CsrfOptions.FromConfiguration(builder.Configuration);
var internalOptions = BoardOilInternalOptions.FromConfiguration(builder.Configuration);
var mcpServiceProviderAccessor = new McpServiceProviderAccessor();

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
builder.Services.AddSingleton(mcpServiceProviderAccessor);
builder.Services.AddSingleton<McpToolDispatcher>();
builder.Services.AddSingleton(new AuthSessionOptions
{
    AccessTokenMinutes = jwtOptions.AccessTokenMinutes,
    RefreshTokenDays = jwtOptions.RefreshTokenDays
});
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
builder.Services.AddScoped<IAuthHttpSessionService, AuthHttpSessionService>();
builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();
builder.Services.AddSingleton<IBoardEvents, BoardRealtimeNotifier>();
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
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = "Bearer realm=\"BoardOil MCP\"";
                await context.Response.WriteAsJsonAsync(CreateMcpAuthError(context.Request, "Invalid or expired bearer token."));
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(BoardOilPolicies.AuthenticatedUser, policy =>
        policy.RequireAuthenticatedUser());
    options.AddPolicy(BoardOilPolicies.McpAuthenticated, policy =>
        policy
            .AddAuthenticationSchemes(McpAuthenticationSchemes.PatBearer)
            .RequireAuthenticatedUser());
    options.AddPolicy(BoardOilPolicies.AdminOnly, policy =>
        policy.RequireRole(BoardOilRoles.Admin));
    options.AddPolicy(BoardOilPolicies.CardEditor, policy =>
        policy.RequireRole(BoardOilRoles.Admin, BoardOilRoles.Standard));
});
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new ModelContextProtocol.Protocol.Implementation
        {
            Name = "BoardOil MCP",
            Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0"
        };
    })
#pragma warning disable MCP9004
    .WithHttpTransport(options =>
    {
        options.Stateless = true;
        options.EnableLegacySse = false;
    })
#pragma warning restore MCP9004
    .WithListToolsHandler((request, cancellationToken) =>
        mcpServiceProviderAccessor
            .ServiceProvider
            .GetRequiredService<McpToolDispatcher>()
            .ListToolsAsync(request, cancellationToken))
    .WithCallToolHandler((request, cancellationToken) =>
        mcpServiceProviderAccessor
            .ServiceProvider
            .GetRequiredService<McpToolDispatcher>()
            .CallToolAsync(request, cancellationToken));

var app = builder.Build();
mcpServiceProviderAccessor.Initialise(app.Services);

if (jwtOptions.AllowInsecureCookies)
{
    app.Logger.LogWarning("Auth cookies are configured with Secure=false. This should only be used for local/home-lab HTTP setups.");
}

await app.Services.InitializeBoardOilAsync();
app.UseCors("BoardOilDevClient");
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/mcp", StringComparison.OrdinalIgnoreCase))
    {
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader)
            || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer realm=\"BoardOil MCP\"";
            await context.Response.WriteAsJsonAsync(CreateMcpAuthError(context.Request, "Missing bearer token."));
            return;
        }
    }

    await next();
});
app.Use(async (context, next) =>
{
    if (IsUnsupportedMcpStylePath(context.Request.Path))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(CreateUnsupportedMcpPathError(context.Request.Path, context.Request));
        return;
    }

    await next();
});
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

app.MapHealthEndpoints();
app.MapBoardEndpoints();
app.MapColumnEndpoints();
app.MapCardEndpoints();
app.MapTagEndpoints();
app.MapInternalRealtimeEndpoints();

app.MapAuthEndpoints();

app.MapHub<BoardHub>("/hubs/board")
    .RequireAuthorization(BoardOilPolicies.AuthenticatedUser);
app.MapMcp("/mcp")
    .RequireAuthorization(BoardOilPolicies.McpAuthenticated);
app.MapGet("/.well-known/mcp", (HttpRequest request) =>
    Results.Json(CreateMcpWellKnownDocument(request)));

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

static bool IsUnsupportedMcpStylePath(PathString path) =>
    path.Equals("/sse", StringComparison.OrdinalIgnoreCase)
    || path.Equals("/messages", StringComparison.OrdinalIgnoreCase)
    || path.Equals("/v1/mcp", StringComparison.OrdinalIgnoreCase);

static object CreateMcpAuthError(HttpRequest request, string detail) =>
    new ApiResult<object>(
        false,
        new
        {
            auth = McpDiscoveryMetadata.CreateAuthMetadata(GetBaseUrl(request)),
            endpoint = $"{GetBaseUrl(request)}/mcp",
            docs = $"{GetBaseUrl(request)}/.well-known/mcp",
            setup = McpDiscoveryMetadata.CreateSetupMetadata(GetBaseUrl(request))
        },
        401,
        detail);

static object CreateUnsupportedMcpPathError(PathString path, HttpRequest request) =>
    new ApiResult<object>(
        false,
        new
        {
            requestedPath = path.ToString(),
            endpoint = $"{GetBaseUrl(request)}/mcp",
            docs = $"{GetBaseUrl(request)}/.well-known/mcp",
            setup = McpDiscoveryMetadata.CreateSetupMetadata(GetBaseUrl(request))
        },
        404,
        "Unsupported MCP endpoint path.");

static object CreateMcpWellKnownDocument(HttpRequest request) =>
    McpDiscoveryMetadata.CreateWellKnownDocument(GetBaseUrl(request));

static string GetBaseUrl(HttpRequest request) =>
    $"{request.Scheme}://{request.Host}";

public partial class Program;
