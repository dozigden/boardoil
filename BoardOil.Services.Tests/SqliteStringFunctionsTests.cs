using System.Globalization;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Ef;
using BoardOil.Ef.Context;
using BoardOil.Ef.DependencyInjection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class SqliteStringFunctionsTests
{
    [Theory]
    [InlineData("en-GB", "café", "CAFÉ")]
    [InlineData("tr-TR", "i", "I")]
    [InlineData("tr-TR", "ı", "ı")]
    [InlineData("en-GB", "ς", "Σ")]
    [InlineData("en-GB", "straße", "STRAßE")]
    [InlineData("en-GB", "cafe\u0301", "CAFE\u0301")]
    [InlineData("en-GB", " [é].*%_ ", " [É].*%_ ")]
    [InlineData("en-GB", "", "")]
    [InlineData("en-GB", null, null)]
    public async Task UpperInvariant_ShouldUseInvariantCasingWithoutOtherNormalisation(
        string cultureName, string? value, string? expected)
    {
        // Arrange
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            await using var db = new BoardOilDbContextFactory("Data Source=:memory:")
                .CreateDbContext<BoardOilDbContext>();
            await db.Database.OpenConnectionAsync();
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT bo_upper_invariant($value)";
            command.Parameters.Add(new SqliteParameter("$value", (object?)value ?? DBNull.Value));

            // Act
            var result = await command.ExecuteScalarAsync();

            // Assert
            Assert.Equal((object?)expected ?? DBNull.Value, result);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData("injected")]
    [InlineData("ambient-factory")]
    [InlineData("tooling")]
    [InlineData("external-open")]
    public async Task ContextCreation_ShouldRegisterFunctionOnEveryPath(string creationPath)
    {
        // Arrange
        await using var provider = new ServiceCollection()
            .AddBoardOilEfInfrastructure("Data Source=:memory:")
            .BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = creationPath switch
        {
            "injected" => scope.ServiceProvider.GetRequiredService<BoardOilDbContext>(),
            "ambient-factory" => provider.GetRequiredService<IDbContextFactory>().CreateDbContext<BoardOilDbContext>(),
            "tooling" => new BoardOilDesignTimeDbContextFactory().CreateDbContext([]),
            "external-open" => new BoardOilDbContext(
                new DbContextOptionsBuilder<BoardOilDbContext>().UseSqlite(connection).Options),
            _ => throw new ArgumentOutOfRangeException(nameof(creationPath))
        };
        if (creationPath == "tooling")
        {
            db.Database.SetConnectionString("Data Source=:memory:");
        }
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT bo_upper_invariant('café'), upper('café')";

        // Act
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        // Assert
        Assert.Equal("CAFÉ", reader.GetString(0));
        Assert.Equal("CAFé", reader.GetString(1));
    }

    [Fact]
    public async Task SharedConnection_ShouldSupportMultipleContexts()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BoardOilDbContext>().UseSqlite(connection).Options;
        await using var first = new BoardOilDbContext(options);

        // Act
        await using var second = new BoardOilDbContext(options);
        await using var comparison = second.Database.GetDbConnection().CreateCommand();
        comparison.CommandText = "SELECT bo_upper_invariant('café')";
        var result = await comparison.ExecuteScalarAsync();

        // Assert
        Assert.Equal("CAFÉ", result);
    }

    [Fact]
    public async Task ReopenedConnection_ShouldRetainFunction()
    {
        // Arrange
        await using var db = new BoardOilDbContextFactory("Data Source=:memory:")
            .CreateDbContext<BoardOilDbContext>();
        await db.Database.OpenConnectionAsync();
        await db.Database.CloseConnectionAsync();
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT bo_upper_invariant('échec')";

        // Act
        var result = await command.ExecuteScalarAsync();

        // Assert
        Assert.Equal("ÉCHEC", result);
    }
}
