using BoardOil.Ef;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class CardCommentIsSystemMigrationIntegrationTests
{
    [Fact]
    public async Task AddCardCommentIsSystem_ShouldPreserveExistingCommentsAsUserComments()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BoardOilDbContext>()
            .UseSqlite(connection)
            .Options;
        const string targetMigration = "20261005180520_AddCardCommentIsSystem";
        var createdAtUtc = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc);
        var postedAtUtc = createdAtUtc.AddDays(2);

        await using (var db = new BoardOilDbContext(options))
        {
            var migrations = db.Database.GetMigrations().ToList();
            var targetIndex = migrations.IndexOf(targetMigration);
            Assert.True(targetIndex > 0);
            await db.Database.MigrateAsync(migrations[targetIndex - 1]);
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO "Boards" ("Id", "Name", "Description", "SlickCohesionModeEnabled", "CreatedAtUtc", "UpdatedAtUtc")
                VALUES (1, 'Board', '', 1, $createdAtUtc, $createdAtUtc);

                INSERT INTO "CardTypes" ("Id", "BoardId", "Name", "StyleName", "StylePropertiesJson", "IsSystem", "CreatedAtUtc", "UpdatedAtUtc")
                VALUES (1, 1, 'Story', 'auto', '{}', 1, $createdAtUtc, $createdAtUtc);

                INSERT INTO "Columns" ("Id", "BoardId", "Title", "SortKey", "CreatedAtUtc", "UpdatedAtUtc")
                VALUES (1, 1, 'Todo', 'A', $createdAtUtc, $createdAtUtc);

                INSERT INTO "Cards" ("Id", "BoardId", "BoardCardId", "BoardColumnId", "CardTypeId", "Title", "Description", "SortKey", "CreatedAtUtc", "UpdatedAtUtc", "CardCreatedUtc", "CardUpdatedUtc")
                VALUES (1, 1, 1, 1, 1, 'Card', '', 'A', $createdAtUtc, $postedAtUtc, $createdAtUtc, $postedAtUtc);

                INSERT INTO "CardComments" ("Id", "CardId", "AuthorUserId", "Text", "PostedAtUtc", "CreatedAtUtc")
                VALUES (1, 1, NULL, 'Existing **comment**', $postedAtUtc, $createdAtUtc);
                """;
            command.Parameters.AddWithValue("$createdAtUtc", createdAtUtc);
            command.Parameters.AddWithValue("$postedAtUtc", postedAtUtc);
            await command.ExecuteNonQueryAsync();
        }

        await using (var db = new BoardOilDbContext(options))
        {
            await db.Database.MigrateAsync(targetMigration);
        }

        await using var assertDb = new BoardOilDbContext(options);
        var comment = await assertDb.CardComments.Select(item => new
        {
            item.Id, item.CardId, item.AuthorUserId, item.Text, item.CreatedAtUtc, item.PostedAtUtc, item.EditedAtUtc, item.IsSystem
        }).SingleAsync();
        Assert.Equal(1, comment.Id);
        Assert.Equal(1, comment.CardId);
        Assert.Null(comment.AuthorUserId);
        Assert.Equal("Existing **comment**", comment.Text);
        Assert.Equal(createdAtUtc, comment.CreatedAtUtc);
        Assert.Equal(postedAtUtc, comment.PostedAtUtc);
        Assert.Null(comment.EditedAtUtc);
        Assert.False(comment.IsSystem);
        var card = await assertDb.Cards.Select(item => new { item.CardCreatedUtc, item.CardUpdatedUtc }).SingleAsync();
        Assert.Equal(createdAtUtc, card.CardCreatedUtc);
        Assert.Equal(postedAtUtc, card.CardUpdatedUtc);
    }
}
