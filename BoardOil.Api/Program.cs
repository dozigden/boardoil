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

app.UseDefaultFiles();
app.UseStaticFiles();

// Frontend SPA fallback once frontend build output is copied into wwwroot.
app.MapFallbackToFile("index.html");

app.Run();
