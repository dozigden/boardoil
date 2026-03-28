using BoardOil.Abstractions;
using BoardOil.Contracts.Realtime;
using BoardOil.Mcp.Server.Configuration;
using BoardOil.Mcp.Server.Mcp;
using BoardOil.Mcp.Server.Realtime;
using BoardOil.Mcp.Server.Tools;
using BoardOil.Services.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var runtimeOptions = McpRuntimeOptions.FromConfiguration(builder.Configuration);
var jwtOptions = McpJwtAuthOptions.FromConfiguration(builder.Configuration);
var serviceProviderAccessor = new McpServiceProviderAccessor();

builder.WebHost.UseUrls(runtimeOptions.HttpUrls);

builder.Services.AddBoardOilServices(runtimeOptions.ConnectionString);
builder.Services.AddSingleton(runtimeOptions);
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton(serviceProviderAccessor);

ConfigureBoardEvents(builder.Services, runtimeOptions);

builder.Services.AddScoped<BoardGetToolHandler>();
builder.Services.AddScoped<ColumnsListToolHandler>();
builder.Services.AddScoped<CardCreateToolHandler>();
builder.Services.AddScoped<CardUpdateToolHandler>();
builder.Services.AddScoped<CardMoveToolHandler>();
builder.Services.AddScoped<CardMoveByColumnNameToolHandler>();
builder.Services.AddScoped<CardDeleteToolHandler>();
builder.Services.AddSingleton<ToolRegistry>();
builder.Services.AddSingleton<McpToolDispatcher>();

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
    });

builder.Services.AddAuthorization();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new ModelContextProtocol.Protocol.Implementation
        {
            Name = "BoardOil MCP Server",
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
        serviceProviderAccessor
            .ServiceProvider
            .GetRequiredService<McpToolDispatcher>()
            .ListToolsAsync(request, cancellationToken))
    .WithCallToolHandler((request, cancellationToken) =>
        serviceProviderAccessor
            .ServiceProvider
            .GetRequiredService<McpToolDispatcher>()
            .CallToolAsync(request, cancellationToken));

var app = builder.Build();
serviceProviderAccessor.Initialise(app.Services);

await app.Services.InitializeBoardOilAsync();
app.UseAuthentication();
app.UseAuthorization();

app.MapMcp("/mcp")
    .RequireAuthorization();

app.Run();

static void ConfigureBoardEvents(IServiceCollection services, McpRuntimeOptions runtimeOptions)
{
    if (runtimeOptions.EventsApiBaseUrl is null)
    {
        services.AddSingleton<IBoardEvents, NoOpBoardEvents>();
        Console.WriteLine("BoardOil MCP realtime forwarding disabled (no API base URL configured).");
        return;
    }

    services.AddSingleton(new HttpClient
    {
        BaseAddress = runtimeOptions.EventsApiBaseUrl,
        Timeout = TimeSpan.FromSeconds(5)
    });
    services.AddSingleton<IBoardEvents>(_ => new ApiForwardingBoardEvents(
        _.GetRequiredService<HttpClient>(),
        runtimeOptions.EventsApiKey));

    var relayTarget = $"{runtimeOptions.EventsApiBaseUrl.ToString().TrimEnd('/')}{BoardRealtimeRelay.EndpointPath}";
    var authMode = string.IsNullOrWhiteSpace(runtimeOptions.EventsApiKey) ? "source-ip" : "api-key";
    Console.WriteLine($"BoardOil MCP realtime forwarding enabled ({authMode}) -> {relayTarget}");
}

public partial class Program;
