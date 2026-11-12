using System.Text.Json;
using BoardOil.Abstractions.Auth;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Auth;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Api.Development;

// Local CLI provisioning only: no HTTP endpoint or alternate authentication handler.
public static class DevMcpSessionCommand
{
    public const string CreateKey = "BoardOilDev:CreateMcpSession";
    public const string RevokeKey = "BoardOilDev:RevokeMcpSession";
    public const string InitialUserName = "dev";
    public const string InitialPassword = "BoardOilDev1234!";

    public static bool IsRequested(IConfiguration configuration) =>
        configuration[CreateKey] is not null || configuration[RevokeKey] is not null;

    public static void ValidateEnvironment(IConfiguration configuration, IHostEnvironment environment)
    {
        if (IsRequested(configuration) && !environment.IsDevelopment())
        {
            throw new InvalidOperationException("MCP test-session provisioning is only available in Development.");
        }
    }

    public static async Task ExecuteAsync(IServiceProvider services, IConfiguration configuration, IHostEnvironment environment)
    {
        ValidateEnvironment(configuration, environment);
        await using var scope = services.CreateAsyncScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        if (configuration[RevokeKey] is { } revokePath)
        {
            var session = JsonSerializer.Deserialize<Session>(await File.ReadAllTextAsync(revokePath))
                ?? throw new InvalidOperationException("Missing MCP test-session credentials.");
            var revoked = await auth.RevokeMachinePatAsync(session.UserId, session.TokenId);
            if (!revoked.Success)
            {
                throw new InvalidOperationException("Could not revoke the MCP test-session token.");
            }
            return;
        }

        var path = configuration[CreateKey] ?? throw new InvalidOperationException("Missing MCP test-session output path.");
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory>();
        await using var db = factory.CreateDbContext<BoardOilDbContext>();
        var initialSetup = !await db.Users.AnyAsync();
        if (initialSetup)
        {
            var registered = await auth.RegisterInitialAdminAsync(new RegisterInitialAdminRequest(
                InitialUserName, "dev@boardoil.test", InitialPassword));
            if (!registered.Success)
            {
                throw new InvalidOperationException("Could not initialise the development account.");
            }
        }

        var user = await db.Users.AsNoTracking()
            .Where(user => user.IsActive && user.Role == UserRole.Admin && user.IdentityType == UserIdentityType.User)
            .OrderBy(user => user.Id).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("The development database needs an active human administrator.");
        var created = await auth.CreateMachinePatAsync(user.Id, new CreateMachinePatRequest(
            "Development MCP test session", 1, [MachinePatScopes.McpRead, MachinePatScopes.McpWrite, MachinePatScopes.McpDiagnosticsRead]));
        if (!created.Success || created.Data is null)
        {
            throw new InvalidOperationException("Could not create the MCP test-session token.");
        }

        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }
            await using var output = new FileStream(path, options);
            await JsonSerializer.SerializeAsync(output, new Session(
                user.Id, created.Data.Token.Id, created.Data.PlainTextToken, user.UserName, initialSetup));
        }
        catch
        {
            await auth.RevokeMachinePatAsync(user.Id, created.Data.Token.Id);
            throw;
        }
    }

    private sealed record Session(int UserId, int TokenId, string Token, string UserName, bool InitialSetup);
}
