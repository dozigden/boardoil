using BoardOil.Data.Abstractions.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests.Infrastructure;

public sealed class SqliteTestHarnessGuardTests : IAsyncLifetime
{
    private SqliteTestHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await SqliteTestHarness.CreateAsync();
    }

    public async ValueTask DisposeAsync()
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

    [Fact]
    public async Task CreateAsync_ShouldProvideAnIsolatedDatabaseFromTemplate()
    {
        // Arrange
        await using var firstHarness = await SqliteTestHarness.CreateAsync();
        await using var secondHarness = await SqliteTestHarness.CreateAsync();
        await using var firstDbContext = firstHarness.CreateDbContext();
        await using var secondDbContext = secondHarness.CreateDbContext();
        firstDbContext.Users.Add(new EntityUser
        {
            UserName = "first-user",
            Email = "first-user@example.com",
            NormalisedEmail = "FIRST-USER@EXAMPLE.COM",
            PasswordHash = "test-hash",
            Role = UserRole.Admin,
            IsActive = true,
        });

        // Act
        await firstDbContext.SaveChangesAsync();
        var secondDatabaseUserCount = await secondDbContext.Users.CountAsync();

        // Assert
        Assert.Equal(0, secondDatabaseUserCount);
    }
}
