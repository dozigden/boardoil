using System.Text.Json;
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
        await db.Database.MigrateAsync(RepairMigration);

        // Assert
        var survivorAfter = await ReadTagAsync(db, survivorId);
        Assert.Equal(survivorBefore with { NormalisedName = "Σ" }, survivorAfter);
        Assert.Equal(asciiBefore, await ReadTagAsync(db, 50));
        Assert.Equal(unaffectedBefore, await SnapshotAsync(connection, "Cards", "ArchivedCards", "Users"));
        var links = await ReadLinks(db).ToListAsync();
        Assert.Equal([1, 2, overlapLinkId, 6], links.Select(x => x.Id));
        Assert.Equal([101, 102, 103, 201], links.Select(x => x.CardId));
        Assert.Equal([survivorId, survivorId, survivorId, 40], links.Select(x => x.TagId));
        Assert.Equal(8, await CountTagsAsync(db));
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
        await db.Database.MigrateAsync(RepairMigration);
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
        await db.Database.MigrateAsync(RepairMigration);

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
        var error = await Assert.ThrowsAsync<SqliteException>(() => db.Database.MigrateAsync(RepairMigration));

        // Assert
        Assert.Contains("Injected tag repair failure", error.Message);
        Assert.Equal(before, await SnapshotAsync(connection, "Tags", "CardTags", "Cards", "ArchivedCards", "Users"));
        Assert.DoesNotContain(RepairMigration, await db.Database.GetAppliedMigrationsAsync());
        await AssertForeignKeysAsync(connection);

        // The migration transaction must also roll back its temporary mapping table.
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER FailTagRepair;");
        await db.Database.MigrateAsync(RepairMigration);
        Assert.Contains(RepairMigration, await db.Database.GetAppliedMigrationsAsync());
        Assert.False(await db.Database.SqlQueryRaw<int>("SELECT Id AS Value FROM Tags WHERE Id = 20").AnyAsync());
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
        Assert.Equal(2, await CountTagsAsync(db));

        // Act
        await db.Database.MigrateAsync(RepairMigration);

        // Assert
        var tag = await ReadTags(db).SingleAsync();
        Assert.Equal(first.ToUpperInvariant(), tag.NormalisedName);
        var link = await ReadLinks(db).SingleAsync();
        Assert.Equal(tag.Id, link.TagId);
        Assert.Equal(1, link.CardId);
        Assert.Equal("Legacy card", await db.Database.SqlQueryRaw<string>("SELECT Title AS Value FROM Cards").SingleAsync());
        await AssertForeignKeysAsync(connection);
    }

    private static async Task SeedAsync(SqliteConnection connection, bool includeCorrectTag)
    {
        await using var db = CreateContext(connection);
        await db.Database.MigrateAsync(PreviousMigration);
        var timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        const string archiveSnapshot = """{"schema":"archived-card","version":1,"payload":{"tagNames":["ς","σ"]}}""";
        await db.Database.ExecuteSqlInterpolatedAsync(
            $$"""
            INSERT INTO Boards (Id, Name, Description, CreatedAtUtc, UpdatedAtUtc)
            VALUES (1, 'First', '', {{timestamp}}, {{timestamp}}), (2, 'Second', '', {{timestamp}}, {{timestamp}});

            INSERT INTO Columns (Id, BoardId, Title, SortKey, CreatedAtUtc, UpdatedAtUtc)
            VALUES (1, 1, 'Todo', 'A', {{timestamp}}, {{timestamp}}), (2, 2, 'Todo', 'A', {{timestamp}}, {{timestamp}});

            INSERT INTO CardTypes (Id, BoardId, Name, IsSystem, StyleName, StylePropertiesJson, CreatedAtUtc, UpdatedAtUtc)
            VALUES (1, 1, 'Default', 1, 'auto', '{}', {{timestamp}}, {{timestamp}}),
                   (2, 2, 'Default', 1, 'auto', '{}', {{timestamp}}, {{timestamp}});

            INSERT INTO Cards (Id, BoardId, BoardCardId, BoardColumnId, CardTypeId, Title, Description, SortKey,
                               CardCreatedUtc, CardUpdatedUtc, CreatedAtUtc, UpdatedAtUtc)
            VALUES (101, 1, 1, 1, 1, 'Card 101', '', '101', {{timestamp}}, {{timestamp}}, {{timestamp}}, {{timestamp}}),
                   (102, 1, 2, 1, 1, 'Card 102', '', '102', {{timestamp}}, {{timestamp}}, {{timestamp}}, {{timestamp}}),
                   (103, 1, 3, 1, 1, 'Card 103', '', '103', {{timestamp}}, {{timestamp}}, {{timestamp}}, {{timestamp}}),
                   (201, 2, 1, 2, 2, 'Card 201', '', '201', {{timestamp}}, {{timestamp}}, {{timestamp}}, {{timestamp}});

            INSERT INTO ArchivedCards (BoardId, OriginalCardId, ArchivedAtUtc, SnapshotJson, SearchTitle, SearchTagsJson, SearchTextNormalised)
            VALUES (1, 999, {{timestamp}}, {{archiveSnapshot}}, 'Archived', '["ς","σ"]', 'ARCHIVED Σ Σ');

            INSERT INTO Users (UserName, DisplayName, Email, NormalisedEmail, Role, IdentityType, IsActive, CreatedAtUtc, UpdatedAtUtc)
            VALUES ('Élodie', 'Élodie', 'Élodie@localhost', 'Élodie@localhost', 0, 0, 1, {{timestamp}}, {{timestamp}});
            """);

        (int Id, int BoardId, string Name, string NormalisedName)[] tags =
        [
            (10, 1, "ς", "ς"), (20, 1, "σ", "σ"), (40, 2, "ς", "ς"), (50, 1, "Bug", "BUG"),
            (60, 1, "café", "CAFé"), (70, 1, "cafe", "CAFE"), (80, 1, "cafe\u0301", "CAFE\u0301"),
            (90, 1, "straße", "STRAßE"), (100, 1, "STRASSE", "STRASSE")
        ];
        if (includeCorrectTag) tags = [..tags, (30, 1, "Σ", "Σ")];
        foreach (var (id, boardId, name, normalisedName) in tags)
        {
            var emoji = id == 30 ? "☀️" : "🌙";
            var styleProperties = $$"""{"presetIndex":{{id / 10 % 3}}}""";
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO Tags (Id, BoardId, Name, NormalisedName, Emoji, StyleName, StylePropertiesJson, CreatedAtUtc, UpdatedAtUtc)
                VALUES ({id}, {boardId}, {name}, {normalisedName}, {emoji}, 'presets', {styleProperties}, {timestamp}, {timestamp});
                """);
        }

        var secondCardTagId = includeCorrectTag ? 30 : 10;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO CardTags (Id, CardId, TagId)
            VALUES (1, 101, 20), (2, 102, {secondCardTagId}), (3, 103, 10), (4, 103, 20), (6, 201, 40);
            """);
        if (includeCorrectTag)
        {
            await db.Database.ExecuteSqlRawAsync("INSERT INTO CardTags (Id, CardId, TagId) VALUES (5, 103, 30);");
        }
    }

    private sealed record TagState(int Id, int BoardId, string Name, string NormalisedName, string? Emoji,
        string StyleName, string StylePropertiesJson, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

    private sealed record CardTagRow(int Id, int CardId, int TagId);

    private static Task<TagState> ReadTagAsync(BoardOilDbContext db, int id) =>
        ReadTags(db).SingleAsync(tag => tag.Id == id);

    private static IQueryable<TagState> ReadTags(BoardOilDbContext db) => db.Database.SqlQueryRaw<TagState>("""
        SELECT Id, BoardId, Name, NormalisedName, Emoji, StyleName, StylePropertiesJson, CreatedAtUtc, UpdatedAtUtc FROM Tags
        """);

    private static IQueryable<CardTagRow> ReadLinks(BoardOilDbContext db) =>
        db.Database.SqlQueryRaw<CardTagRow>("SELECT Id, CardId, TagId FROM CardTags").OrderBy(link => link.Id);

    private static Task<int> CountTagsAsync(BoardOilDbContext db) =>
        db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Tags").SingleAsync();

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
