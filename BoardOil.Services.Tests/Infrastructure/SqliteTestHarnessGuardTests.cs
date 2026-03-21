using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests.Infrastructure;

public sealed class SqliteTestHarnessGuardTests : IAsyncLifetime
{
    private SqliteTestHarness _harness = null!;

    public async Task InitializeAsync()
    {
        _harness = await SqliteTestHarness.CreateAsync();
    }

    public async Task DisposeAsync()
    {
        await _harness.DisposeAsync();
    }

    [Fact]
    public void CreateDbContext_ShouldUseInMemorySqliteConnection()
    {
        // Arrange
        using var dbContext = _harness.CreateDbContext();

        // Act
        var dbConnection = dbContext.Database.GetDbConnection();

        // Assert
        var sqliteConnection = Assert.IsType<SqliteConnection>(dbConnection);
        Assert.Contains(":memory:", sqliteConnection.ConnectionString, StringComparison.Ordinal);
        Assert.DoesNotContain(".db", sqliteConnection.ConnectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateDbContext_ShouldReuseSharedOpenConnection()
    {
        // Arrange
        using var first = _harness.CreateDbContext();
        using var second = _harness.CreateDbContext();

        // Act
        var firstConnection = first.Database.GetDbConnection();
        var secondConnection = second.Database.GetDbConnection();

        // Assert
        Assert.Same(firstConnection, secondConnection);
        Assert.Equal(System.Data.ConnectionState.Open, firstConnection.State);
    }
}
