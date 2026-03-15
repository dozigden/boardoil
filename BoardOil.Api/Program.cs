using BoardOil.Api.Configuration;
using BoardOil.Api.Extensions;
using BoardOil.Api.Realtime;
using BoardOil.Services.Abstractions;
using BoardOil.Services.Contracts;
using BoardOil.Services.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
var runtimeOptions = BoardOilRuntimeOptions.FromConfiguration(builder.Configuration);

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
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<IBoardEvents, BoardRealtimeNotifier>();
builder.Services.AddSingleton<ITypingPresenceService, TypingPresenceService>();
builder.Services.AddHostedService<TypingPresenceExpiryService>();

var app = builder.Build();

await app.Services.InitializeBoardOilAsync();
app.UseCors("BoardOilDevClient");

// API health endpoint used for container/dev smoke checks.
app.MapGet("/api/health", () => ApiResults.Ok(new { status = "ok" }).ToHttpResult());

app.MapGet("/api/board", (IBoardService boardService) =>
    boardService.GetBoardAsync().ToHttpResult());

app.MapGet("/api/columns", (IColumnService columnService) =>
    columnService.GetColumnsAsync().ToHttpResult());

app.MapPost("/api/columns", (CreateColumnRequest request, IColumnService columnService) =>
    columnService.CreateColumnAsync(request).ToHttpResult());

app.MapPatch("/api/columns/{id:int}", (int id, UpdateColumnRequest request, IColumnService columnService) =>
    columnService.UpdateColumnAsync(id, request).ToHttpResult());

app.MapDelete("/api/columns/{id:int}", (int id, IColumnService columnService) =>
    columnService.DeleteColumnAsync(id).ToHttpResult());

app.MapPost("/api/cards", (CreateCardRequest request, ICardService cardService) =>
    cardService.CreateCardAsync(request).ToHttpResult());

app.MapPatch("/api/cards/{id:int}", (int id, UpdateCardRequest request, ICardService cardService) =>
    cardService.UpdateCardAsync(id, request).ToHttpResult());

app.MapDelete("/api/cards/{id:int}", (int id, ICardService cardService) =>
    cardService.DeleteCardAsync(id).ToHttpResult());

app.MapHub<BoardHub>("/hubs/board");

app.UseDefaultFiles();
app.UseStaticFiles();

// Frontend SPA fallback once frontend build output is copied into wwwroot.
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
