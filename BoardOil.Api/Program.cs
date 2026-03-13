using BoardOil.Api.Extensions;
using BoardOil.Services.DependencyInjection;
using BoardOil.Services.Contracts;
using BoardOil.Services.Abstractions;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("BoardOil")
    ?? "Data Source=/data/boardoil.db";

builder.Services.AddBoardOilServices(connectionString);

var app = builder.Build();

await app.Services.InitializeBoardOilAsync();

// API health endpoint used for container/dev smoke checks.
app.MapGet("/api/health", () => ApiResults.Ok(new { status = "ok" }).ToHttpResult());

app.MapGet("/api/board", (IBoardService boardService, CancellationToken cancellationToken) =>
    boardService.GetBoardAsync(cancellationToken).ToHttpResult());

app.MapGet("/api/columns", (IColumnService columnService, CancellationToken cancellationToken) =>
    columnService.GetColumnsAsync(cancellationToken).ToHttpResult());

app.MapPost("/api/columns", (CreateColumnRequest request, IColumnService columnService, CancellationToken cancellationToken) =>
    columnService.CreateColumnAsync(request, cancellationToken).ToHttpResult());

app.MapPatch("/api/columns/{id:int}", (int id, UpdateColumnRequest request, IColumnService columnService, CancellationToken cancellationToken) =>
    columnService.UpdateColumnAsync(id, request, cancellationToken).ToHttpResult());

app.MapDelete("/api/columns/{id:int}", (int id, IColumnService columnService, CancellationToken cancellationToken) =>
    columnService.DeleteColumnAsync(id, cancellationToken).ToHttpResult());

app.MapPost("/api/cards", (CreateCardRequest request, ICardService cardService, CancellationToken cancellationToken) =>
    cardService.CreateCardAsync(request, cancellationToken).ToHttpResult());

app.MapPatch("/api/cards/{id:int}", (int id, UpdateCardRequest request, ICardService cardService, CancellationToken cancellationToken) =>
    cardService.UpdateCardAsync(id, request, cancellationToken).ToHttpResult());

app.MapDelete("/api/cards/{id:int}", (int id, ICardService cardService, CancellationToken cancellationToken) =>
    cardService.DeleteCardAsync(id, cancellationToken).ToHttpResult());

app.UseDefaultFiles();
app.UseStaticFiles();

// Frontend SPA fallback once frontend build output is copied into wwwroot.
app.MapFallbackToFile("index.html");

app.Run();
