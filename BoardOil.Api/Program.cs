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
app.MapGet("/api/health", () =>
{
    var result = ApiResult.Ok(new { status = "ok" });
    return Results.Json(result, statusCode: result.StatusCode);
});

app.MapGet("/api/columns", async (IColumnService columnService, CancellationToken cancellationToken) =>
{
    var result = await columnService.GetColumnsAsync(cancellationToken);
    return Results.Json(result, statusCode: result.StatusCode);
});

app.MapPost("/api/columns", async (CreateColumnRequest request, IColumnService columnService, CancellationToken cancellationToken) =>
{
    var result = await columnService.CreateColumnAsync(request, cancellationToken);
    return Results.Json(result, statusCode: result.StatusCode);
});

app.MapPatch("/api/columns/{id:int}", async (int id, UpdateColumnRequest request, IColumnService columnService, CancellationToken cancellationToken) =>
{
    var result = await columnService.UpdateColumnAsync(id, request, cancellationToken);
    return Results.Json(result, statusCode: result.StatusCode);
});

app.MapDelete("/api/columns/{id:int}", async (int id, IColumnService columnService, CancellationToken cancellationToken) =>
{
    var result = await columnService.DeleteColumnAsync(id, cancellationToken);
    return Results.Json(result, statusCode: result.StatusCode);
});

app.UseDefaultFiles();
app.UseStaticFiles();

// Frontend SPA fallback once frontend build output is copied into wwwroot.
app.MapFallbackToFile("index.html");

app.Run();
