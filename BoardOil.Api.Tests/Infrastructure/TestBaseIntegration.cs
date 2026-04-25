using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace BoardOil.Api.Tests.Infrastructure;

public abstract class TestBaseIntegration : ApiFactoryIntegrationTestBase
{
    protected HttpClient Client { get; private set; } = null!;
    protected string HubAccessToken { get; private set; } = string.Empty;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        Client = CreateClient();
        await AuthenticateAsInitialAdminAsync();
    }

    protected async Task AuthenticateAsInitialAdminAsync()
    {
        HubAccessToken = await AuthenticateAsInitialAdminAsync(Client);
    }

    protected static async Task AuthenticateAsInitialAdminAsync(HttpClient client, IServiceProvider serviceProvider)
    {
        _ = await AdminAuthenticationHelper.AuthenticateAsSeededAdminAsync(client, serviceProvider);
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

}
