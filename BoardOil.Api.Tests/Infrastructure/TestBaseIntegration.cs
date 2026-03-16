using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace BoardOil.Api.Tests.Infrastructure;

public abstract class TestBaseIntegration : IAsyncLifetime
{
    protected virtual string DbNamePrefix => "boardoil-api-tests";
    protected virtual int TypingTtlSeconds => 2;

    protected string DatabasePath { get; private set; } = string.Empty;
    protected BoardOilApiFactory Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    public Task InitializeAsync()
    {
        DatabasePath = BuildDbPath(DbNamePrefix);
        Factory = new BoardOilApiFactory(DatabasePath, TypingTtlSeconds);
        Client = Factory.CreateClient();
        return AuthenticateAsInitialAdminAsync(Client);
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }
    }

    protected static string CreateDbPath(string dbNamePrefix) => BuildDbPath(dbNamePrefix);

    protected static async Task AuthenticateAsInitialAdminAsync(HttpClient client)
    {
        var register = await client.PostAsJsonAsync("/api/auth/register-initial-admin", new RegisterInitialAdminRequest("admin", "Password1234!"));
        if (register.StatusCode == HttpStatusCode.Conflict)
        {
            var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "Password1234!"));
            login.EnsureSuccessStatusCode();
            return;
        }

        register.EnsureSuccessStatusCode();
    }

    protected HubConnection CreateHubConnection()
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(Factory.Server.BaseAddress!, "/hubs/board"), options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }

    protected static async Task<T> WaitAsync<T>(Task<T> task, TimeSpan? timeout = null)
    {
        var wait = timeout ?? TimeSpan.FromSeconds(3);
        var completed = await Task.WhenAny(task, Task.Delay(wait));
        if (completed != task)
        {
            throw new TimeoutException($"Timed out waiting for event after {wait}.");
        }

        return await task;
    }

    private static string BuildDbPath(string dbNamePrefix)
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), ".test-data");
        Directory.CreateDirectory(root);
        return Path.Combine(root, $"{dbNamePrefix}-{Guid.NewGuid():N}.db");
    }

    private sealed record RegisterInitialAdminRequest(string UserName, string Password);
    private sealed record LoginRequest(string UserName, string Password);
}
