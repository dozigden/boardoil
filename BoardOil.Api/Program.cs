using BoardOil.Api.Auth;
using BoardOil.Api.Configuration;
using BoardOil.Api.Extensions;
using BoardOil.Api.Realtime;
using BoardOil.Services.Abstractions;
using BoardOil.Services.Auth;
using BoardOil.Services.Contracts;
using BoardOil.Services.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var runtimeOptions = BoardOilRuntimeOptions.FromConfiguration(builder.Configuration);
var jwtOptions = JwtAuthOptions.FromConfiguration(builder.Configuration);
var csrfOptions = CsrfOptions.FromConfiguration(builder.Configuration);

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
builder.Services.AddSingleton(runtimeOptions);
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton(csrfOptions);
builder.Services.AddSingleton(new AuthSessionOptions
{
    AccessTokenMinutes = jwtOptions.AccessTokenMinutes,
    RefreshTokenDays = jwtOptions.RefreshTokenDays
});
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
builder.Services.AddScoped<IAuthHttpSessionService, AuthHttpSessionService>();
builder.Services.AddSingleton<IBoardEvents, BoardRealtimeNotifier>();
builder.Services.AddSingleton<ITypingPresenceService, TypingPresenceService>();
builder.Services.AddHostedService<TypingPresenceExpiryService>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(BoardOilPolicies.AuthenticatedUser, policy =>
        policy.RequireAuthenticatedUser());
    options.AddPolicy(BoardOilPolicies.AdminOnly, policy =>
        policy.RequireRole(BoardOilRoles.Admin));
    options.AddPolicy(BoardOilPolicies.CardEditor, policy =>
        policy.RequireRole(BoardOilRoles.Admin, BoardOilRoles.Standard));
});

var app = builder.Build();

await app.Services.InitializeBoardOilAsync();
app.UseCors("BoardOilDevClient");
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

    if (!context.Request.Cookies.TryGetValue(jwtOptions.AccessTokenCookieName, out var accessToken)
        || string.IsNullOrWhiteSpace(accessToken))
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
app.UseAuthentication();
app.UseAuthorization();

// API health endpoint used for container/dev smoke checks.
app.MapGet("/api/health", () => ApiResults.Ok(new { status = "ok" }).ToHttpResult());

app.MapGet("/api/board", (IBoardService boardService) =>
    boardService.GetBoardAsync().ToHttpResult())
    .RequireAuthorization(BoardOilPolicies.AuthenticatedUser);

app.MapGet("/api/columns", (IColumnService columnService) =>
    columnService.GetColumnsAsync().ToHttpResult())
    .RequireAuthorization(BoardOilPolicies.AdminOnly);

app.MapPost("/api/columns", (CreateColumnRequest request, IColumnService columnService) =>
    columnService.CreateColumnAsync(request).ToHttpResult())
    .RequireAuthorization(BoardOilPolicies.AdminOnly);

app.MapPatch("/api/columns/{id:int}", (int id, UpdateColumnRequest request, IColumnService columnService) =>
    columnService.UpdateColumnAsync(id, request).ToHttpResult())
    .RequireAuthorization(BoardOilPolicies.AdminOnly);

app.MapDelete("/api/columns/{id:int}", (int id, IColumnService columnService) =>
    columnService.DeleteColumnAsync(id).ToHttpResult())
    .RequireAuthorization(BoardOilPolicies.AdminOnly);

app.MapPost("/api/cards", (CreateCardRequest request, ICardService cardService) =>
    cardService.CreateCardAsync(request).ToHttpResult())
    .RequireAuthorization(BoardOilPolicies.CardEditor);

app.MapPatch("/api/cards/{id:int}", (int id, UpdateCardRequest request, ICardService cardService) =>
    cardService.UpdateCardAsync(id, request).ToHttpResult())
    .RequireAuthorization(BoardOilPolicies.CardEditor);

app.MapDelete("/api/cards/{id:int}", (int id, ICardService cardService) =>
    cardService.DeleteCardAsync(id).ToHttpResult())
    .RequireAuthorization(BoardOilPolicies.CardEditor);

app.MapAuthEndpoints();

app.MapHub<BoardHub>("/hubs/board");

app.UseDefaultFiles();
app.UseStaticFiles();

// Frontend SPA fallback once frontend build output is copied into wwwroot.
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
