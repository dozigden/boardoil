using Xunit;

namespace BoardOil.Api.Tests.Infrastructure;

public abstract class ApiFactoryIntegrationTestBase : IAsyncLifetime
{
    private readonly List<HttpClient> clients = [];
    private IResettableApiFactoryFixture? sharedFixture;

    protected virtual string DbNamePrefix => GetType().Name;

    protected string DatabasePath { get; private set; } = string.Empty;
    protected BoardOilApiFactory Factory { get; private set; } = null!;

    public virtual ValueTask InitializeAsync()
    {
        if (sharedFixture is not null)
        {
            DatabasePath = sharedFixture.DatabasePath;
            Factory = sharedFixture.Factory;
            return sharedFixture.ResetAsync();
        }

        DatabasePath = BuildDbPath(DbNamePrefix);
        Factory = CreateFactory(DatabasePath);
        return ValueTask.CompletedTask;
    }

    public virtual async ValueTask DisposeAsync()
    {
        foreach (var client in clients)
        {
            client.Dispose();
        }
        clients.Clear();

        if (sharedFixture is null && Factory is not null)
        {
            await Factory.DisposeAsync();
        }
    }

    protected HttpClient CreateClient() => TrackClient(Factory.CreateClient());

    protected HttpClient TrackClient(HttpClient client)
    {
        clients.Add(client);
        return client;
    }

    protected void UseSharedFactory(IResettableApiFactoryFixture fixture)
    {
        sharedFixture = fixture;
    }

    protected Task<string> AuthenticateAsInitialAdminAsync(HttpClient client) =>
        AdminAuthenticationHelper.AuthenticateAsSeededAdminAsync(client, Factory.Services);

    protected Task EnsureInitialAdminSeededAsync() =>
        AdminAuthenticationHelper.EnsureAdminSeededAsync(Factory.Services);

    protected static string CreateDbPath(string dbNamePrefix) => BuildDbPath(dbNamePrefix);

    protected virtual BoardOilApiFactory CreateFactory(string databasePath) =>
        new(databasePath);

    internal static string BuildDbPath(string dbNamePrefix)
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), ".test-data");
        Directory.CreateDirectory(root);
        return Path.Combine(root, $"{dbNamePrefix}-{Guid.NewGuid():N}.db");
    }
}

public interface IResettableApiFactoryFixture
{
    string DatabasePath { get; }
    BoardOilApiFactory Factory { get; }
    ValueTask ResetAsync();
}

public sealed class DefaultApiFactoryFixture : IAsyncLifetime, IResettableApiFactoryFixture
{
    public DefaultApiFactoryFixture()
    {
        DatabasePath = ApiFactoryIntegrationTestBase.BuildDbPath(nameof(DefaultApiFactoryFixture));
        Factory = new BoardOilApiFactory(DatabasePath);
    }

    public string DatabasePath { get; }
    public BoardOilApiFactory Factory { get; }

    public ValueTask InitializeAsync()
    {
        using var client = Factory.CreateClient();
        return ValueTask.CompletedTask;
    }

    public ValueTask ResetAsync()
    {
        Factory.ResetDatabaseFromTemplate();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
    }
}
