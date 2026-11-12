using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class UserExperimentalFeaturesMigrationIntegrationTests
{
    [Fact]
    public async Task AddUserExperimentalFeaturesEnabled_ShouldDefaultExistingAndNewUsersToFalse()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BoardOilDbContext>().UseSqlite(connection).Options;
        var createdAtUtc = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

        await using (var db = new BoardOilDbContext(options))
        {
            await db.Database.MigrateAsync("20261109151910_UseSingleUniqueUserEmail");
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Users" ("UserName", "DisplayName", "Email", "PasswordHash", "Role", "IdentityType", "IsActive", "CreatedAtUtc", "UpdatedAtUtc")
                VALUES ({"existing"}, {"Existing User"}, {"existing@example.test"}, {"hash"}, 0, 0, 1, {createdAtUtc}, {createdAtUtc});
                """);
        }

        await using (var db = new BoardOilDbContext(options))
        {
            await db.Database.MigrateAsync("20261112114202_AddUserExperimentalFeaturesEnabled");
        }

        await using var assertDb = new BoardOilDbContext(options);
        var existing = await assertDb.Users.SingleAsync();
        Assert.False(existing.ExperimentalFeaturesEnabled);
        Assert.Equal("Existing User", existing.DisplayName);
        Assert.Equal("existing@example.test", existing.Email);
        Assert.Equal(createdAtUtc, existing.UpdatedAtUtc);

        var newUser = new EntityUser { UserName = "new", DisplayName = "New User", Email = "new@example.test" };
        assertDb.Users.Add(newUser);
        await assertDb.SaveChangesAsync();
        await assertDb.Entry(newUser).ReloadAsync();
        Assert.False(newUser.ExperimentalFeaturesEnabled);
    }
}
