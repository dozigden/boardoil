using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace BoardOil.Api.Tests.Infrastructure;

public abstract class TestBaseIntegration : IAsyncLifetime
{
    private const string AccessCookieName = "boardoil_access";

    protected virtual string DbNamePrefix => "boardoil-api-tests";

    protected string DatabasePath { get; private set; } = string.Empty;
    protected BoardOilApiFactory Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;
    protected string HubAccessToken { get; private set; } = string.Empty;

    public Task InitializeAsync()
    {
        DatabasePath = BuildDbPath(DbNamePrefix);
        Factory = new BoardOilApiFactory(DatabasePath);
        Client = Factory.CreateClient();
        return AuthenticateAsInitialAdminAsync();
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }
    }

    protected static string CreateDbPath(string dbNamePrefix) => BuildDbPath(dbNamePrefix);

    protected async Task AuthenticateAsInitialAdminAsync()
    {
        var client = Client;
        var register = await client.PostAsJsonAsync("/api/auth/register-initial-admin", new RegisterInitialAdminRequest("admin", "Password1234!"));
        if (register.StatusCode == HttpStatusCode.Conflict)
        {
            var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "Password1234!"));
            login.EnsureSuccessStatusCode();
            HubAccessToken = TryGetCookieValue(login, AccessCookieName) ?? string.Empty;
            var loginEnvelope = await login.Content.ReadFromJsonAsync<ApiEnvelope<AuthSessionEnvelope>>();
            Assert.NotNull(loginEnvelope);
            Assert.NotNull(loginEnvelope!.Data);
            SetCsrfHeader(client, loginEnvelope.Data!.CsrfToken);
            return;
        }

        register.EnsureSuccessStatusCode();
        HubAccessToken = TryGetCookieValue(register, AccessCookieName) ?? string.Empty;
        var registerEnvelope = await register.Content.ReadFromJsonAsync<ApiEnvelope<AuthSessionEnvelope>>();
        Assert.NotNull(registerEnvelope);
        Assert.NotNull(registerEnvelope!.Data);
        SetCsrfHeader(client, registerEnvelope.Data!.CsrfToken);
    }

    protected static async Task AuthenticateAsInitialAdminAsync(HttpClient client)
    {
        var register = await client.PostAsJsonAsync("/api/auth/register-initial-admin", new RegisterInitialAdminRequest("admin", "Password1234!"));
        if (register.StatusCode == HttpStatusCode.Conflict)
        {
            var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "Password1234!"));
            login.EnsureSuccessStatusCode();
            var loginEnvelope = await login.Content.ReadFromJsonAsync<ApiEnvelope<AuthSessionEnvelope>>();
            Assert.NotNull(loginEnvelope);
            Assert.NotNull(loginEnvelope!.Data);
            SetCsrfHeader(client, loginEnvelope.Data!.CsrfToken);
            return;
        }

        register.EnsureSuccessStatusCode();
        var registerEnvelope = await register.Content.ReadFromJsonAsync<ApiEnvelope<AuthSessionEnvelope>>();
        Assert.NotNull(registerEnvelope);
        Assert.NotNull(registerEnvelope!.Data);
        SetCsrfHeader(client, registerEnvelope.Data!.CsrfToken);
    }

    private static void SetCsrfHeader(HttpClient client, string csrfToken)
    {
        client.DefaultRequestHeaders.Remove("X-BoardOil-CSRF");
        client.DefaultRequestHeaders.Add("X-BoardOil-CSRF", csrfToken);
    }

    protected HubConnection CreateHubConnection(bool authenticated = true)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(Factory.Server.BaseAddress!, "/hubs/board"), options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                if (authenticated)
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(HubAccessToken);
                }
            })
            .Build();
    }

    private static string? TryGetCookieValue(HttpResponseMessage response, string cookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return null;
        }

        var prefix = $"{cookieName}=";
        foreach (var value in values)
        {
            if (!value.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var end = value.IndexOf(';');
            var token = end >= 0 ? value[prefix.Length..end] : value[prefix.Length..];
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }
        }

        return null;
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
    private sealed record AuthSessionEnvelope(string CsrfToken);
    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
}
