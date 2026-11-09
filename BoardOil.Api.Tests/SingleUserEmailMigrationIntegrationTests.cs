using System.Globalization;
using System.Text.Json;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class SingleUserEmailMigrationIntegrationTests
{
    private const string PreviousMigration = "20261108191836_RepairTagNameNormalisation";
    private const string TargetMigration = "20261109151910_UseSingleUniqueUserEmail";
    private static readonly string[] RelatedTables = ["BoardMembers", "Cards", "CardComments", "PersonalAccessTokens", "RefreshTokens"];

    [Fact]
    public async Task Upgrade_ShouldAllocateUniqueAddressesWithoutChangingAccountIdentityOrArchives()
    {
        // Arrange
        await using var connection = await OpenAsync();
        await using var db = Context(connection);
        await SeedAsync(db);
        var accountsBefore = await ReadAsync(connection, "SELECT Id,UserName,DisplayName,PasswordHash,Role,IdentityType,IsActive,CreatedAtUtc,UpdatedAtUtc FROM Users ORDER BY Id");
        var relationsBefore = await SnapshotAsync(connection, RelatedTables);
        var archivesBefore = await SnapshotAsync(connection, "ArchivedCards");

        // Act
        await db.Database.MigrateAsync();

        // Assert
        Assert.Equal(new[] { "café@example.com", "CAFÉ+3@example.com", "café+1@example.com", "Café+4@example.com",
            "CAFÉ+2@example.com", "CAFÉ+1+1@example.com", "unrelated@example.net", "CAFÉ+5@example.com" },
            await db.Users.OrderBy(x => x.Id).Select(x => x.Email).ToArrayAsync());
        Assert.Equal(accountsBefore, await ReadAsync(connection, "SELECT Id,UserName,DisplayName,PasswordHash,Role,IdentityType,IsActive,CreatedAtUtc,UpdatedAtUtc FROM Users ORDER BY Id"));
        Assert.Equal(relationsBefore, await SnapshotAsync(connection, RelatedTables));
        Assert.Equal(archivesBefore, await SnapshotAsync(connection, "ArchivedCards"));
        Assert.Equal("[]", await ReadAsync(connection, "SELECT name FROM pragma_table_info('Users') WHERE name='NormalisedEmail'"));
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal("[]", await ReadAsync(connection, "PRAGMA foreign_key_check"));
    }

    [Theory]
    [InlineData(false, "en-GB")]
    [InlineData(false, "tr-TR")]
    [InlineData(true, "en-GB")]
    [InlineData(true, "tr-TR")]
    public async Task Database_ShouldRejectEquivalentEmailsOnInsertAndUpdate(bool freshSchema, string culture)
    {
        // Arrange
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            await using var connection = await OpenAsync();
            await using var db = Context(connection);
            if (freshSchema) await db.Database.EnsureCreatedAsync();
            else await db.Database.MigrateAsync();
            await InsertCurrentAsync(db, 1, "Café@EXAMPLE.COM");
            await InsertCurrentAsync(db, 2, "distinct@example.com");

            // Act
            var insertError = await Assert.ThrowsAsync<SqliteException>(() => InsertCurrentAsync(db, 3, " café@example.com\u00a0"));
            var updateError = await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("UPDATE Users SET Email='CAFÉ@example.com' WHERE Id=2"));

            // Assert
            Assert.Equal(2067, insertError.SqliteExtendedErrorCode);
            Assert.Equal(2067, updateError.SqliteExtendedErrorCode);
            Assert.Equal(1, (await db.Users.SingleAsync(x => x.Email == "CAFÉ@example.com")).Id);
            Assert.Equal("Café@EXAMPLE.COM", (await db.Users.SingleAsync(x => x.Id == 1)).Email);
            Assert.Equal("distinct@example.com", (await db.Users.SingleAsync(x => x.Id == 2)).Email);
            Assert.Contains("IX_Users_Email", await ReadAsync(connection, "EXPLAIN QUERY PLAN SELECT Id FROM Users WHERE Email='CAFÉ@example.com'"));
            using var emailDefault = JsonDocument.Parse(await ReadAsync(connection, "SELECT dflt_value FROM pragma_table_info('Users') WHERE name='Email'"));
            Assert.Equal("''", emailDefault.RootElement[0][0].GetString());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task Database_ShouldPreserveAccentAndCompositionDistinctionsAndUseInvariantCasing()
    {
        // Arrange
        await using var connection = await OpenAsync();
        await using var db = Context(connection);
        await db.Database.EnsureCreatedAsync();

        // Act
        await InsertCurrentAsync(db, 1, "café@example.com");
        await InsertCurrentAsync(db, 2, "cafe@example.com");
        await InsertCurrentAsync(db, 3, "cafe\u0301@example.com");
        await InsertCurrentAsync(db, 4, "I@example.com");
        var error = await Assert.ThrowsAsync<SqliteException>(() => InsertCurrentAsync(db, 5, "i@example.com"));

        // Assert
        Assert.Equal(4, await db.Users.CountAsync());
        Assert.Equal(2067, error.SqliteExtendedErrorCode);
    }

    [Fact]
    public async Task Upgrade_WhenIndexCreationFails_ShouldRollBackRepairAndSchemaThenAllowRetry()
    {
        // Arrange
        await using var connection = await OpenAsync();
        await using var db = Context(connection);
        await SeedAsync(db);
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IX_Users_Email ON Users(UserName)");
        var before = await SnapshotAsync(connection, ["Users", "ArchivedCards", .. RelatedTables]);

        // Act
        var error = await Assert.ThrowsAsync<SqliteException>(() => db.Database.MigrateAsync());

        // Assert
        Assert.Contains("already exists", error.Message);
        Assert.Equal(before, await SnapshotAsync(connection, ["Users", "ArchivedCards", .. RelatedTables]));
        Assert.DoesNotContain(TargetMigration, await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal("[]", await ReadAsync(connection, "SELECT name FROM sqlite_temp_master WHERE name='__UserEmailRepair'"));
        await db.Database.ExecuteSqlRawAsync("DROP INDEX IX_Users_Email");
        await db.Database.MigrateAsync();
        Assert.Contains(TargetMigration, await db.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task Upgrade_WhenRepeatedOrDowngradedAndReapplied_ShouldPreserveRepairedAddresses()
    {
        // Arrange
        await using var connection = await OpenAsync();
        await using var db = Context(connection);
        await SeedAsync(db);
        await db.Database.MigrateAsync();
        var addresses = await ReadAsync(connection, "SELECT Id,Email FROM Users ORDER BY Id");
        var archives = await SnapshotAsync(connection, "ArchivedCards");

        // Act
        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync(PreviousMigration);
        await db.Database.MigrateAsync();

        // Assert
        Assert.Equal(addresses, await ReadAsync(connection, "SELECT Id,Email FROM Users ORDER BY Id"));
        Assert.Equal(archives, await SnapshotAsync(connection, "ArchivedCards"));
        Assert.Equal("[]", await ReadAsync(connection, "PRAGMA foreign_key_check"));
    }

    [Fact]
    public async Task Upgrade_FromBeforeEmailBackfill_ShouldRepairGeneratedAddresses()
    {
        // Arrange
        await using var connection = await OpenAsync();
        await using var db = Context(connection);
        var migrations = db.Database.GetMigrations().ToList();
        await db.Database.MigrateAsync(migrations[migrations.IndexOf("20260423184443_AddUserEmailAddress") - 1]);
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Users (Id,UserName,PasswordHash,Role,IdentityType,IsActive,CreatedAtUtc,UpdatedAtUtc)
            VALUES (1,'Élodie','first-password',0,0,1,'2026-01-01','2026-01-01'),(2,'élodie','second-password',0,0,1,'2026-01-01','2026-01-01');
            """);

        // Act
        await db.Database.MigrateAsync();

        // Assert
        Assert.Equal(["Élodie@localhost", "élodie+1@localhost"], await db.Users.OrderBy(x => x.Id).Select(x => x.Email).ToArrayAsync());
        Assert.Equal(["first-password", "second-password"], await db.Users.OrderBy(x => x.Id).Select(x => x.PasswordHash).ToArrayAsync());
    }

    [Fact]
    public async Task ConnectionWithoutCollation_ShouldRejectWrites()
    {
        // Arrange
        var connectionString = $"Data Source=email-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var registered = new SqliteConnection(connectionString);
        await registered.OpenAsync();
        await using var db = Context(registered);
        await db.Database.EnsureCreatedAsync();
        await using var unregistered = new SqliteConnection(connectionString);
        await unregistered.OpenAsync();
        await using var command = unregistered.CreateCommand();
        command.CommandText = "INSERT INTO Users (UserName,DisplayName,Email,Role,IdentityType,IsActive,CreatedAtUtc,UpdatedAtUtc) VALUES ('raw','Raw','raw@example.com',0,0,1,'2026-01-01','2026-01-01')";

        // Act
        var error = await Assert.ThrowsAsync<SqliteException>(() => command.ExecuteNonQueryAsync());

        // Assert
        Assert.Contains("BO_EMAIL", error.Message);
        Assert.Equal(0, await db.Users.CountAsync());
    }

    [Theory]
    [InlineData("Owner@example.com")]
    [InlineData("Élodie@localhost")]
    public async Task Upgrade_WithOneUser_ShouldKeepTheAddress(string email)
    {
        // Arrange
        await using var connection = await OpenAsync();
        await using var db = Context(connection);
        await db.Database.MigrateAsync(PreviousMigration);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO Users (Id,UserName,DisplayName,Email,NormalisedEmail,Role,IdentityType,IsActive,CreatedAtUtc,UpdatedAtUtc)
            VALUES (1,'owner','Owner',{email},'old-normalisation',0,0,1,'2026-01-01','2026-01-01');
            """);

        // Act
        await db.Database.MigrateAsync();

        // Assert
        Assert.Equal(email, (await db.Users.SingleAsync()).Email);
        Assert.Equal(1, (await db.Users.SingleAsync(x => x.Email == email.ToLowerInvariant())).Id);
    }

    private static async Task SeedAsync(BoardOilDbContext db)
    {
        await db.Database.MigrateAsync(PreviousMigration);
        string[] emails = ["café@example.com", "CAFÉ@example.com", "café+1@example.com", "Café@example.com",
            "CAFÉ+2@example.com", "CAFÉ+1@example.com", "unrelated@example.net", "CAFÉ@example.com"];
        for (var i = 0; i < emails.Length; i++)
        {
            var id = i + 1;
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO Users (Id,UserName,DisplayName,Email,NormalisedEmail,PasswordHash,Role,IdentityType,IsActive,CreatedAtUtc,UpdatedAtUtc)
                VALUES ({id},{$"user-{id}"},{$"User {id}"},{emails[i]},{$"legacy-{id}"},{$"hash-{id}"},0,0,1,'2026-01-01','2026-01-01');
                """);
        }
        db.Boards.Add(new EntityBoard { Id = 1, Name = "Board" });
        db.Columns.Add(new EntityBoardColumn { Id = 1, BoardId = 1, Title = "Todo", SortKey = "A" });
        db.CardTypes.Add(new EntityCardType { Id = 1, BoardId = 1, Name = "Default", IsSystem = true, StyleName = "auto", StylePropertiesJson = "{}" });
        db.Cards.Add(new EntityBoardCard { Id = 1, BoardId = 1, BoardCardId = 1, BoardColumnId = 1, CardTypeId = 1, Title = "Card", SortKey = "A", AssignedUserId = 2 });
        db.CardComments.Add(new EntityCardComment { CardId = 1, AuthorUserId = 4, Text = "Live comment" });
        db.BoardMembers.Add(new EntityBoardMember { BoardId = 1, UserId = 2, Role = BoardMemberRole.Owner });
        db.PersonalAccessTokens.Add(new EntityPersonalAccessToken { UserId = 2, Name = "Token", TokenHash = "unchanged-token", TokenPrefix = "bo_pat", ScopesCsv = "mcp:read" });
        db.RefreshTokens.Add(new EntityRefreshToken { UserId = 2, TokenHash = "unchanged-refresh", ExpiresAtUtc = DateTime.UtcNow.AddDays(1) });
        string[] snapshots = [
            """{"schema":"archived-card","version":1,"payload":{"assignedUserId":2,"assignedUserEmail":"CAFÉ@example.com","unknownField":"preserve me","comments":[{"authorUserId":4,"authorEmail":"Café@example.com","text":"Keep this text"},{"authorUserId":8,"authorEmail":"CAFÉ@example.com"},{"authorUserId":999,"authorEmail":"CAFÉ@example.com"}]}}""",
            "{invalid", """{"schema":"archived-card","version":99,"payload":{"assignedUserEmail":"CAFÉ@example.com"}}""",
            """{"schema":"archived-card","version":1,"payload":{"assignedUserEmail":"CAFÉ@example.com","extra":1,"extra":2}}"""
        ];
        for (var i = 0; i < snapshots.Length; i++)
        {
            db.ArchivedCards.Add(new EntityArchivedCard { BoardId = 1, OriginalCardId = 99 + i, ArchivedAtUtc = DateTime.UtcNow, SnapshotJson = snapshots[i], SearchTitle = "Archive", SearchTagsJson = "[]", SearchTextNormalised = "ARCHIVE" });
        }
        await db.SaveChangesAsync();
    }

    private static Task<int> InsertCurrentAsync(BoardOilDbContext db, int id, string email) => db.Database.ExecuteSqlInterpolatedAsync(
        $"""
        INSERT INTO Users (Id,UserName,DisplayName,Email,Role,IdentityType,IsActive,CreatedAtUtc,UpdatedAtUtc)
        VALUES ({id},{$"user-{id}"},'User',{email},0,0,1,'2026-01-01','2026-01-01');
        """);

    private static async Task<string> SnapshotAsync(SqliteConnection connection, params string[] tables)
    {
        var snapshots = new List<string>();
        foreach (var table in tables) snapshots.Add(await ReadAsync(connection, $"SELECT * FROM \"{table}\" ORDER BY Id"));
        return JsonSerializer.Serialize(snapshots);
    }

    private static async Task<string> ReadAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<object[]>();
        while (await reader.ReadAsync())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            rows.Add(values);
        }
        return JsonSerializer.Serialize(rows);
    }

    private static BoardOilDbContext Context(SqliteConnection connection) => new(new DbContextOptionsBuilder<BoardOilDbContext>().UseSqlite(connection).Options);

    private static async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }
}
