using System.Text.Json;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using BoardOil.Ef.Migrations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class TagNameRepairMigrationIntegrationTests
{
    private const string PreviousMigration = "20261004085210_AddCardCommentEditedAtUtc";
    private const string RepairMigration = "20261108191836_RepairTagNameNormalisation";

    [Theory]
    [InlineData(false, 10, 3)]
    [InlineData(true, 30, 5)]
    public async Task Upgrade_ShouldMergeWithinBoardAndPreserveSurvivorAndCardLinks(
        bool includeCorrectTag, int survivorId, int overlapLinkId)
    {
        // Arrange
        await using var connection = await OpenConnectionAsync();
        await SeedAsync(connection, includeCorrectTag);
        await using var db = CreateContext(connection);
        var survivorBefore = await ReadTagAsync(db, survivorId);
        var asciiBefore = await ReadTagAsync(db, 50);
        var unaffectedBefore = await SnapshotAsync(connection, "Cards", "ArchivedCards", "Users");

        // Act
        await db.Database.MigrateAsync();

        // Assert
        var survivorAfter = await ReadTagAsync(db, survivorId);
        Assert.Equal(survivorBefore with { NormalisedName = "Σ" }, survivorAfter);
        Assert.Equal(asciiBefore, await ReadTagAsync(db, 50));
        Assert.Equal(unaffectedBefore, await SnapshotAsync(connection, "Cards", "ArchivedCards", "Users"));
        var links = await db.CardTags.OrderBy(x => x.Id).Select(x => new { x.Id, x.CardId, x.TagId }).ToListAsync();
        Assert.Equal([1, 2, overlapLinkId, 6], links.Select(x => x.Id));
        Assert.Equal([101, 102, 103, 201], links.Select(x => x.CardId));
        Assert.Equal([survivorId, survivorId, survivorId, 40], links.Select(x => x.TagId));
        Assert.Equal(8, await db.Tags.CountAsync());
        Assert.Equal("Σ", (await ReadTagAsync(db, 40)).NormalisedName);
        Assert.Equal(2, (await ReadTagAsync(db, 40)).BoardId);
        Assert.Equal("CAFÉ", (await ReadTagAsync(db, 60)).NormalisedName);
        Assert.Equal("CAFE", (await ReadTagAsync(db, 70)).NormalisedName);
        Assert.Equal("CAFE\u0301", (await ReadTagAsync(db, 80)).NormalisedName);
        Assert.Equal("STRAßE", (await ReadTagAsync(db, 90)).NormalisedName);
        Assert.Equal("STRASSE", (await ReadTagAsync(db, 100)).NormalisedName);
        Assert.False(db.Database.HasPendingModelChanges());
        await AssertForeignKeysAsync(connection);
    }

    [Fact]
    public async Task RepairSql_WhenRepeated_ShouldLeaveAllDataUnchanged()
    {
        // Arrange
        await using var connection = await OpenConnectionAsync();
        await SeedAsync(connection, true);
        await using var db = CreateContext(connection);
        await db.Database.MigrateAsync();
        var before = await SnapshotAsync(connection, "Tags", "CardTags", "Cards", "ArchivedCards", "Users");

        // Act
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            foreach (var operation in new RepairTagNameNormalisation().UpOperations.Cast<SqlOperation>())
            {
                await db.Database.ExecuteSqlRawAsync(operation.Sql);
            }
            await transaction.CommitAsync();
        }
        await db.Database.MigrateAsync();

        // Assert
        Assert.Equal(before, await SnapshotAsync(connection, "Tags", "CardTags", "Cards", "ArchivedCards", "Users"));
        await AssertForeignKeysAsync(connection);
    }

    [Fact]
    public async Task Upgrade_WhenTagDeletionFails_ShouldRollBackAllRepairsAndAllowRetry()
    {
        // Arrange
        await using var connection = await OpenConnectionAsync();
        await SeedAsync(connection, false);
        await using var db = CreateContext(connection);
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER FailTagRepair BEFORE DELETE ON Tags WHEN OLD.Id = 20
            BEGIN SELECT RAISE(ABORT, 'Injected tag repair failure'); END;
            """);
        var before = await SnapshotAsync(connection, "Tags", "CardTags", "Cards", "ArchivedCards", "Users");

        // Act
        var error = await Assert.ThrowsAsync<SqliteException>(() => db.Database.MigrateAsync());

        // Assert
        Assert.Contains("Injected tag repair failure", error.Message);
        Assert.Equal(before, await SnapshotAsync(connection, "Tags", "CardTags", "Cards", "ArchivedCards", "Users"));
        Assert.DoesNotContain(RepairMigration, await db.Database.GetAppliedMigrationsAsync());
        await AssertForeignKeysAsync(connection);

        // The migration transaction must also roll back its temporary mapping table.
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER FailTagRepair;");
        await db.Database.MigrateAsync();
        Assert.Contains(RepairMigration, await db.Database.GetAppliedMigrationsAsync());
        Assert.False(await db.Tags.AnyAsync(x => x.Id == 20));
    }

    [Theory]
    [InlineData("café", "CAFÉ")]
    [InlineData("ς", "σ")]
    public async Task Upgrade_FromLegacyFreeTypedTags_ShouldRepairHistoricalBackfill(string first, string second)
    {
        // Arrange
        await using var connection = await OpenConnectionAsync();
        await using var db = CreateContext(connection);
        await db.Database.MigrateAsync("20260328202823_AddMcpPublicBaseUrlSetting");
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Boards (Id,Name,CreatedAtUtc,UpdatedAtUtc) VALUES (1,'Legacy','2026-01-01','2026-01-01');
            INSERT INTO Columns (Id,BoardId,Title,SortKey,CreatedAtUtc,UpdatedAtUtc) VALUES (1,1,'Todo','A','2026-01-01','2026-01-01');
            INSERT INTO Cards (Id,BoardColumnId,Title,Description,SortKey,CreatedAtUtc,UpdatedAtUtc)
                VALUES (1,1,'Legacy card','','A','2026-01-01','2026-01-01');
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO CardTags (CardId,TagName) VALUES (1,{first}), (1,{second});");
        await db.Database.MigrateAsync(PreviousMigration);
        Assert.Equal(2, await db.Tags.CountAsync());

        // Act
        await db.Database.MigrateAsync();

        // Assert
        var tag = await db.Tags.SingleAsync();
        Assert.Equal(first.ToUpperInvariant(), tag.NormalisedName);
        var link = await db.CardTags.SingleAsync();
        Assert.Equal(tag.Id, link.TagId);
        Assert.Equal(1, link.CardId);
        Assert.Equal("Legacy card", (await db.Cards.SingleAsync()).Title);
        await AssertForeignKeysAsync(connection);
    }

    private static async Task SeedAsync(SqliteConnection connection, bool includeCorrectTag)
    {
        await using var db = CreateContext(connection);
        await db.Database.MigrateAsync(PreviousMigration);
        db.Boards.AddRange(new EntityBoard { Id = 1, Name = "First" }, new EntityBoard { Id = 2, Name = "Second" });
        db.Columns.AddRange(
            new EntityBoardColumn { Id = 1, BoardId = 1, Title = "Todo", SortKey = "A" },
            new EntityBoardColumn { Id = 2, BoardId = 2, Title = "Todo", SortKey = "A" });
        db.CardTypes.AddRange(
            new EntityCardType { Id = 1, BoardId = 1, Name = "Default", IsSystem = true, StyleName = "auto", StylePropertiesJson = "{}" },
            new EntityCardType { Id = 2, BoardId = 2, Name = "Default", IsSystem = true, StyleName = "auto", StylePropertiesJson = "{}" });
        db.Tags.AddRange(
            Tag(10, 1, "ς", "ς"), Tag(20, 1, "σ", "σ"), Tag(40, 2, "ς", "ς"),
            Tag(50, 1, "Bug", "BUG"), Tag(60, 1, "café", "CAFé"), Tag(70, 1, "cafe", "CAFE"),
            Tag(80, 1, "cafe\u0301", "CAFE\u0301"), Tag(90, 1, "straße", "STRAßE"), Tag(100, 1, "STRASSE", "STRASSE"));
        if (includeCorrectTag) db.Tags.Add(Tag(30, 1, "Σ", "Σ"));
        foreach (var cardId in new[] { 101, 102, 103, 201 })
        {
            var boardId = cardId / 100;
            db.Cards.Add(new EntityBoardCard
            {
                Id = cardId, BoardId = boardId, BoardCardId = cardId % 100,
                BoardColumnId = boardId, CardTypeId = boardId, Title = $"Card {cardId}", SortKey = cardId.ToString()
            });
        }
        db.CardTags.AddRange(
            Link(1, 101, 20), Link(2, 102, includeCorrectTag ? 30 : 10),
            Link(3, 103, 10), Link(4, 103, 20), Link(6, 201, 40));
        if (includeCorrectTag) db.CardTags.Add(Link(5, 103, 30));
        db.ArchivedCards.Add(new EntityArchivedCard
        {
            BoardId = 1, OriginalCardId = 999, ArchivedAtUtc = DateTime.UtcNow,
            SnapshotJson = """{"schema":"archived-card","version":1,"payload":{"tagNames":["ς","σ"]}}""",
            SearchTitle = "Archived", SearchTagsJson = """["ς","σ"]""", SearchTextNormalised = "ARCHIVED Σ Σ"
        });
        db.Users.Add(new EntityUser
        {
            UserName = "Élodie", DisplayName = "Élodie", Email = "Élodie@localhost", NormalisedEmail = "Élodie@localhost"
        });
        await db.SaveChangesAsync();
    }

    private static EntityTag Tag(int id, int boardId, string name, string normalisedName) => new()
    {
        Id = id, BoardId = boardId, Name = name, NormalisedName = normalisedName,
        Emoji = id == 30 ? "☀️" : "🌙", StyleName = "presets", StylePropertiesJson = $"{{\"presetIndex\":{id / 10 % 3}}}"
    };

    private static EntityCardTag Link(int id, int cardId, int tagId) => new() { Id = id, CardId = cardId, TagId = tagId };

    private sealed record TagState(int Id, int BoardId, string Name, string NormalisedName, string? Emoji,
        string StyleName, string StylePropertiesJson, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

    private static Task<TagState> ReadTagAsync(BoardOilDbContext db, int id) => db.Tags.Where(x => x.Id == id)
        .Select(x => new TagState(x.Id, x.BoardId, x.Name, x.NormalisedName, x.Emoji, x.StyleName,
            x.StylePropertiesJson, x.CreatedAtUtc, x.UpdatedAtUtc)).SingleAsync();

    private static async Task<string> SnapshotAsync(SqliteConnection connection, params string[] tables)
    {
        var rows = new List<object[]>();
        foreach (var table in tables)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM \"{table}\" ORDER BY Id";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var values = new object[reader.FieldCount];
                reader.GetValues(values);
                rows.Add(values);
            }
        }
        return JsonSerializer.Serialize(rows);
    }

    private static async Task AssertForeignKeysAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.False(await reader.ReadAsync());
    }

    private static BoardOilDbContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<BoardOilDbContext>().UseSqlite(connection).Options);

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }
}
