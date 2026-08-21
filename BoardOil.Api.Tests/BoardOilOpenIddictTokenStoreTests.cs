using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Api.Configuration;
using BoardOil.Api.OAuth;
using BoardOil.Ef;
using BoardOil.Ef.DependencyInjection;
using BoardOil.Ef.OpenIddict;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using Xunit;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BoardOil.Api.Tests;

public sealed class BoardOilOpenIddictTokenStoreTests
{
    [Fact]
    public async Task UpdateAsync_WhenSqliteWriteIsLocked_ShouldLeaveContextUsable()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var databasePath = ApiFactoryIntegrationTestBase.BuildDbPath(
            nameof(BoardOilOpenIddictTokenStoreTests));
        var connectionString = $"Data Source={databasePath};Default Timeout=1;Pooling=False";
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBoardOilEfInfrastructure(connectionString);
        services.AddBoardOilOAuth(new JwtAuthOptions
        {
            SigningKey = BoardOilApiFactory.DefaultSigningKey,
            AllowInsecureCookies = true
        });

        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BoardOilDbContext>();
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var token = NewToken(TokenTypeIdentifiers.RefreshToken);
        dbContext.Add(token);
        await dbContext.SaveChangesAsync(cancellationToken);

        var store = scope.ServiceProvider.GetRequiredService<
            IOpenIddictTokenStore<OpenIddictEntityFrameworkCoreToken>>();
        Assert.IsType<BoardOilOpenIddictTokenStore>(store);

        await using var lockingConnection = new SqliteConnection(connectionString);
        await lockingConnection.OpenAsync(cancellationToken);
        await ExecuteAsync(lockingConnection, "BEGIN EXCLUSIVE;", cancellationToken);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(async () =>
            await store.UpdateAsync(token, cancellationToken));
        await ExecuteAsync(lockingConnection, "ROLLBACK;", cancellationToken);

        // Assert
        var sqliteException = Assert.IsType<SqliteException>(exception.InnerException);
        Assert.Equal(5, sqliteException.SqliteErrorCode);
        Assert.Equal(EntityState.Unchanged, dbContext.Entry(token).State);

        var replacement = NewToken(TokenTypeIdentifiers.AccessToken);
        await store.CreateAsync(replacement, cancellationToken);
        Assert.Equal(EntityState.Unchanged, dbContext.Entry(replacement).State);
    }

    private static OpenIddictEntityFrameworkCoreToken NewToken(string type) =>
        new()
        {
            Id = Guid.NewGuid().ToString(),
            ConcurrencyToken = Guid.NewGuid().ToString(),
            Status = Statuses.Valid,
            Subject = "1",
            Type = type
        };

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
