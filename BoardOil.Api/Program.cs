using BoardOil.Api.Auth;
using BoardOil.Api.Configuration;
using BoardOil.Api.Endpoints;
using BoardOil.Api.ErrorLogs;
using BoardOil.Api.Extensions;
using BoardOil.Api.Mcp;
using BoardOil.Api.Middleware;
using BoardOil.Api.OAuth;
using BoardOil.Api.Realtime;
using BoardOil.Api.Swagger;
using BoardOil.Abstractions;
using BoardOil.Abstractions.Auth;
using BoardOil.Abstractions.Image;
using BoardOil.Contracts.Common;
using BoardOil.Ef.DependencyInjection;
using BoardOil.Services.DependencyInjection;
using BoardOil.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);
var runtimeOptions = BoardOilRuntimeOptions.FromConfiguration(builder.Configuration);
var connectionString = runtimeOptions.ResolveConnectionString(builder.Configuration);
var signingKeyPath = runtimeOptions.ResolveSigningKeyPath(connectionString);
var signingKey = JwtSigningKeyProvider.Resolve(builder.Configuration, signingKeyPath);
var jwtOptions = JwtAuthOptions.FromConfiguration(builder.Configuration, signingKey);
var csrfOptions = CsrfOptions.FromConfiguration(builder.Configuration);
var internalOptions = BoardOilInternalOptions.FromConfiguration(builder.Configuration);
var mcpOptions = BoardOilMcpOptions.FromConfiguration(builder.Configuration);
var buildInfo = BoardOilBuildInfo.FromConfiguration(builder.Configuration, builder.Environment, typeof(Program).Assembly);

builder.WebHost.UseUrls(runtimeOptions.ResolveListenUrl(builder.Configuration));

var imageStorageOptions = BoardOilImageStorageOptions.Resolve(builder.Configuration, connectionString);
builder.Services.AddBoardOilServices();
builder.Services.AddBoardOilEfInfrastructure(connectionString);
builder.Services.AddBoardOilOAuth(jwtOptions);
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "boardoil_oauth_antiforgery";
    options.FormFieldName = "boardoil_oauth_antiforgery";
});
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
builder.Services.AddSingleton(imageStorageOptions);
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton(csrfOptions);
builder.Services.AddSingleton(internalOptions);
builder.Services.AddSingleton(mcpOptions);
builder.Services.AddSingleton(buildInfo);
builder.Services.AddBoardOilMcp(mcpOptions);
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
builder.Services.AddScoped<IAuthorizationHandler, McpOAuthConnectionAuthorizationHandler>();
builder.Services.AddBoardOilAuthentication(jwtOptions);
builder.Services.AddAuthorization(options =>
{
    options.AddBoardOilAuthorizationPolicies(mcpOptions);
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SupportNonNullableReferenceTypes();
    options.SchemaFilter<NonNullableRequestSchemaFilter>();
    options.SchemaFilter<CardSearchSchemaFilter>();
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
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "PAT",
        Description = "Personal access token. Paste the raw `bo_pat_...` value; Swagger UI will add `Bearer`."
    });
    options.OperationFilter<PatSecurityOperationFilter>();
});
var app = builder.Build();
app.InitialiseMcpServiceProvider();

app.LogMcpStartupWarnings();

await app.Services.InitializeBoardOilEfInfrastructureAsync();
await app.Services.PurgeExpiredErrorLogsAsync();
app.UseCors("BoardOilDevClient");
app.UseMiddleware<ApiExceptionLoggingMiddleware>();
app.UseRateLimiter();
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
app.UseMcpOAuthScopeEnforcement();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", $"BoardOil API {buildInfo.Version}");
    options.UseRequestInterceptor("(req) => { req.credentials = 'omit'; return req; }");
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
app.MapSlickEndpoints();
app.MapInternalRealtimeEndpoints();
app.MapConfigurationEndpoints();
app.MapSystemInfoMessageEndpoints();
app.MapErrorLogEndpoints();
app.MapUserEndpoints();
app.MapClientAccountEndpoints();
app.MapOAuthConnectionEndpoints();
app.MapOAuthEndpoints();

app.MapAuthEndpoints();

app.MapHub<BoardHub>("/hubs/board")
    .RequireAuthorization(BoardOilPolicies.AuthenticatedUser);

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    RequestPath = "/images",
    FileProvider = new PhysicalFileProvider(imageStorageOptions.RootPath),
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    }
});
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (string.Equals(context.File.Name, "index.html", StringComparison.OrdinalIgnoreCase))
        {
            ApplySpaShellCacheHeaders(context.Context.Response);
        }
    }
});

// Frontend SPA fallback once frontend build output is copied into wwwroot.
app.MapFallback(async context =>
{
    var webRootPath = app.Environment.WebRootPath;
    if (string.IsNullOrWhiteSpace(webRootPath))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    var indexFilePath = Path.Combine(webRootPath, "index.html");
    if (!File.Exists(indexFilePath))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    ApplySpaShellCacheHeaders(context.Response);
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(indexFilePath);
});

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

static void ApplySpaShellCacheHeaders(HttpResponse response)
{
    response.Headers.CacheControl = "no-cache, must-revalidate";
    response.Headers.Pragma = "no-cache";
}

public partial class Program;
