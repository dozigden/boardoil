var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// API health endpoint used for container/dev smoke checks.
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.UseDefaultFiles();
app.UseStaticFiles();

// Frontend SPA fallback once frontend build output is copied into wwwroot.
app.MapFallbackToFile("index.html");

app.Run();
