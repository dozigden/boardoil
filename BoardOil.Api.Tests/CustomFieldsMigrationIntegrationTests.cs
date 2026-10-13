using BoardOil.Ef;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class CustomFieldsMigrationIntegrationTests
{
    [Fact]
    public async Task AddCustomFields_ShouldInitialiseEmptyJsonWithoutChangingExistingCardsOrTypes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BoardOilDbContext>().UseSqlite(connection).Options;
        const string targetMigration = "20261004123656_AddCustomFields";
        var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var updated = created.AddDays(1);
        await using (var db = new BoardOilDbContext(options))
        {
            var migrations = db.Database.GetMigrations().ToList();
            var index = migrations.IndexOf(targetMigration);
            Assert.True(index > 0);
            await db.Database.MigrateAsync(migrations[index - 1]);
        }
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO "Boards" ("Id", "Name", "Description", "SlickCohesionModeEnabled", "CreatedAtUtc", "UpdatedAtUtc")
                VALUES (1, 'Board', '', 1, $created, $updated);
                INSERT INTO "CardTypes" ("Id", "BoardId", "Name", "StyleName", "StylePropertiesJson", "IsSystem", "CreatedAtUtc", "UpdatedAtUtc")
                VALUES (1, 1, 'Story', 'auto', '{}', 1, $created, $updated);
                INSERT INTO "Columns" ("Id", "BoardId", "Title", "SortKey", "CreatedAtUtc", "UpdatedAtUtc")
                VALUES (1, 1, 'Todo', 'A', $created, $updated);
                INSERT INTO "Cards" ("Id", "BoardId", "BoardCardId", "BoardColumnId", "CardTypeId", "Title", "Description", "SortKey", "CreatedAtUtc", "UpdatedAtUtc", "CardCreatedUtc", "CardUpdatedUtc")
                VALUES (1, 1, 7, 1, 1, 'Existing', 'Keep description', 'A', $created, $updated, $created, $updated);
                """;
            command.Parameters.AddWithValue("$created", created);
            command.Parameters.AddWithValue("$updated", updated);
            await command.ExecuteNonQueryAsync();
        }

        await using (var db = new BoardOilDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        await using var assertDb = new BoardOilDbContext(options);
        var card = await assertDb.Cards.SingleAsync();
        var type = await assertDb.CardTypes.SingleAsync();
        Assert.Equal("{}", card.CustomFieldValuesJson);
        Assert.Empty(await assertDb.CustomFields.ToListAsync());
        Assert.Empty(await assertDb.CustomFieldCardTypes.ToListAsync());
        Assert.Equal(7, card.BoardCardId);
        Assert.Equal("Existing", card.Title);
        Assert.Equal("Keep description", card.Description);
        Assert.Equal(created, card.CreatedAtUtc);
        Assert.Equal(updated, card.UpdatedAtUtc);
        Assert.Equal(created, card.CardCreatedUtc);
        Assert.Equal(updated, card.CardUpdatedUtc);
        Assert.Equal("Story", type.Name);
        Assert.Equal("{}", type.StylePropertiesJson);
        Assert.Equal(updated, type.UpdatedAtUtc);
        Assert.False(assertDb.Database.HasPendingModelChanges());
    }
}
