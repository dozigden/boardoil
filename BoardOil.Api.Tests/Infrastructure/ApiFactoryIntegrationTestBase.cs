using Xunit;

namespace BoardOil.Api.Tests.Infrastructure;

public abstract class ApiFactoryIntegrationTestBase : IAsyncLifetime
{
    protected virtual string DbNamePrefix => GetType().Name;

    protected string DatabasePath { get; private set; } = string.Empty;
    protected BoardOilApiFactory Factory { get; private set; } = null!;

    public virtual Task InitializeAsync()
    {
        DatabasePath = BuildDbPath(DbNamePrefix);
        Factory = CreateFactory(DatabasePath);
        return Task.CompletedTask;
    }

    public virtual async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }
    }

    protected HttpClient CreateClient() => Factory.CreateClient();

    protected Task<string> AuthenticateAsInitialAdminAsync(HttpClient client) =>
        AdminAuthenticationHelper.AuthenticateAsSeededAdminAsync(client, Factory.Services);

    protected Task EnsureInitialAdminSeededAsync() =>
        AdminAuthenticationHelper.EnsureAdminSeededAsync(Factory.Services);

    protected static string CreateDbPath(string dbNamePrefix) => BuildDbPath(dbNamePrefix);

    protected virtual BoardOilApiFactory CreateFactory(string databasePath) =>
        new(databasePath);

    private static string BuildDbPath(string dbNamePrefix)
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), ".test-data");
        Directory.CreateDirectory(root);
        return Path.Combine(root, $"{dbNamePrefix}-{Guid.NewGuid():N}.db");
    }
}
