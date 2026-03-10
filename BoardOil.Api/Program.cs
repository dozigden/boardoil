using BoardOil.Services.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("BoardOil")
    ?? "Data Source=/data/boardoil.db";

builder.Services.AddBoardOilServices(connectionString);

var app = builder.Build();

await app.Services.InitializeBoardOilAsync();

// API health endpoint used for container/dev smoke checks.
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.UseDefaultFiles();
app.UseStaticFiles();

// Frontend SPA fallback once frontend build output is copied into wwwroot.
app.MapFallbackToFile("index.html");

app.Run();
