using BoardOil.Ef;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class AnnouncementMigrationIntegrationTests
{
    [Fact]
    public async Task AnnounceSlickEffects_ShouldNotifyOnlyHumanAccountsPresentAtUpgrade()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BoardOilDbContext>().UseSqlite(connection).Options;
        await using var db = new BoardOilDbContext(options);
        await db.Database.MigrateAsync("20261118085203_AddBoardDeletedAtUtc");
        await InsertAccountAsync(db, "existing", 0);
        await InsertAccountAsync(db, "client", 1);

        await db.Database.MigrateAsync("20261121074812_AnnounceSlickEffects");
        await InsertAccountAsync(db, "new", 0);
        await db.Database.MigrateAsync("20261121074812_AnnounceSlickEffects");

        Assert.Equal(["existing"], await db.Users.Where(x => x.HasUnreadAnnouncement).Select(x => x.UserName).ToArrayAsync());
    }

    private static Task InsertAccountAsync(BoardOilDbContext db, string name, int identityType) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Users (UserName, DisplayName, Email, Role, IdentityType, IsActive, CreatedAtUtc, UpdatedAtUtc)
            VALUES ({name}, {name}, {name + "@localhost"}, 0, {identityType}, 1, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            """);
}
