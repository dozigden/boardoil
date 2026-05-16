using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Auth;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Data.Abstractions.Card;
using BoardOil.Data.Abstractions.CardType;
using BoardOil.Data.Abstractions.Column;
using BoardOil.Data.Abstractions.Configuration;
using BoardOil.Data.Abstractions.Image;
using BoardOil.Data.Abstractions.Slick;
using BoardOil.Data.Abstractions.Tag;
using BoardOil.Data.Abstractions.Users;
using BoardOil.Ef.Context;
using BoardOil.Ef.Repositories;
using BoardOil.Ef.Scope;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace BoardOil.Ef.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBoardOilEfInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IDbContextFactory>(_ => new BoardOilDbContextFactory(connectionString));
        services.AddTransient<IDbContextScopeFactory, DbContextScopeFactory>();
        services.AddTransient<IAmbientDbContextLocator, AmbientDbContextLocator>();
        services.AddScoped<IBoardRepository, BoardRepository>();
        services.AddScoped<IBoardMemberRepository, BoardMemberRepository>();
        services.AddScoped<IAuthUserRepository, AuthUserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPersonalAccessTokenRepository, PersonalAccessTokenRepository>();
        services.AddScoped<IAppSettingRepository, AppSettingRepository>();
        services.AddScoped<IColumnRepository, ColumnRepository>();
        services.AddScoped<ICardRepository, CardRepository>();
        services.AddScoped<ICardCommentRepository, CardCommentRepository>();
        services.AddScoped<IArchivedCardRepository, ArchivedCardRepository>();
        services.AddScoped<ICardTypeRepository, CardTypeRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<ISlickRepository, SlickRepository>();
        services.AddScoped<IImageRepository, ImageRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        return services;
    }

    public static async Task InitializeBoardOilEfInfrastructureAsync(this IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory>();
        await using var dbContext = dbContextFactory.CreateDbContext<BoardOilDbContext>();
        var hasMigrations = dbContext.Database.GetMigrations().Any();
        if (hasMigrations)
        {
            var hasPendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync()).Any();
            if (hasPendingMigrations)
            {
                await BackupDatabaseBeforeMigrationAsync(dbContext);
            }

            await EnsureLegacyEnsureCreatedDatabaseCanMigrateAsync(dbContext);
            await dbContext.Database.MigrateAsync();
            DeleteExpiredDatabaseBackups(dbContext, TimeSpan.FromDays(30));
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        var bootstrapper = scope.ServiceProvider.GetRequiredService<IBoardBootstrapService>();
        await bootstrapper.EnsureDefaultBoardAsync();
    }

    private static async Task EnsureLegacyEnsureCreatedDatabaseCanMigrateAsync(BoardOilDbContext dbContext)
    {
        var hasHistoryTable = await TableExistsAsync(dbContext, "__EFMigrationsHistory");
        if (hasHistoryTable)
        {
            return;
        }

        var hasLegacyBoardTables =
            await TableExistsAsync(dbContext, "Boards")
            && await TableExistsAsync(dbContext, "Columns")
            && await TableExistsAsync(dbContext, "Cards");
        if (!hasLegacyBoardTables)
        {
            return;
        }

        // Legacy path: database was created with EnsureCreated() before migrations existed.
        // Create auth tables explicitly, then mark the first migration as applied.
        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Users" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY AUTOINCREMENT,
                "UserName" TEXT NOT NULL,
                "DisplayName" TEXT NOT NULL DEFAULT '',
                "Email" TEXT NOT NULL DEFAULT '',
                "NormalisedEmail" TEXT NOT NULL DEFAULT '',
                "PasswordHash" TEXT NOT NULL,
                "Role" INTEGER NOT NULL,
                "IdentityType" INTEGER NOT NULL DEFAULT 0,
                "IsActive" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_UserName" ON "Users" ("UserName");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_NormalisedEmail" ON "Users" ("NormalisedEmail");
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "RefreshTokens" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_RefreshTokens" PRIMARY KEY AUTOINCREMENT,
                "UserId" INTEGER NOT NULL,
                "TokenHash" TEXT NOT NULL,
                "ExpiresAtUtc" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "RevokedAtUtc" TEXT NULL,
                "ReplacedByTokenHash" TEXT NULL,
                CONSTRAINT "FK_RefreshTokens_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RefreshTokens_TokenHash" ON "RefreshTokens" ("TokenHash");
            CREATE INDEX IF NOT EXISTS "IX_RefreshTokens_UserId" ON "RefreshTokens" ("UserId");
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            """);

        var firstMigration = dbContext.Database.GetMigrations().First();
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT @migrationId, @productVersion
            WHERE NOT EXISTS (
                SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = @migrationId
            );
            """,
            new SqliteParameter("@migrationId", firstMigration),
            new SqliteParameter("@productVersion", "10.0.4"));
    }

    private static async Task<bool> TableExistsAsync(BoardOilDbContext dbContext, string tableName)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync();
        return result is not null && result != DBNull.Value;
    }

    private static async Task BackupDatabaseBeforeMigrationAsync(BoardOilDbContext dbContext)
    {
        var databasePath = ResolveSqliteDatabasePath(dbContext);
        if (databasePath is null || !File.Exists(databasePath))
        {
            return;
        }

        var databaseDirectory = Path.GetDirectoryName(databasePath);
        if (string.IsNullOrWhiteSpace(databaseDirectory))
        {
            return;
        }

        var backupDirectory = Path.Combine(databaseDirectory, "backups");
        Directory.CreateDirectory(backupDirectory);

        var extension = Path.GetExtension(databasePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".db";
        }

        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH-mm-ss.fffffff'Z'", CultureInfo.InvariantCulture);
        var backupFileName = $"boardoil-backup-{timestamp}{extension}";
        var backupPath = Path.Combine(backupDirectory, backupFileName);

        await using var source = new FileStream(databasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 81920, useAsync: true);
        await using var destination = new FileStream(backupPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
        await source.CopyToAsync(destination);
    }

    private static void DeleteExpiredDatabaseBackups(BoardOilDbContext dbContext, TimeSpan retentionPeriod)
    {
        var databasePath = ResolveSqliteDatabasePath(dbContext);
        if (databasePath is null)
        {
            return;
        }

        var databaseDirectory = Path.GetDirectoryName(databasePath);
        if (string.IsNullOrWhiteSpace(databaseDirectory))
        {
            return;
        }

        var backupDirectory = Path.Combine(databaseDirectory, "backups");
        if (!Directory.Exists(backupDirectory))
        {
            return;
        }

        var cutoffUtc = DateTimeOffset.UtcNow - retentionPeriod;
        foreach (var backupPath in Directory.EnumerateFiles(backupDirectory, "boardoil-backup-*"))
        {
            var backupCreatedAt = ParseBackupTimestampUtc(backupPath);
            if (backupCreatedAt is null || backupCreatedAt >= cutoffUtc)
            {
                continue;
            }

            File.Delete(backupPath);
        }
    }

    private static string? ResolveSqliteDatabasePath(BoardOilDbContext dbContext)
    {
        var sqliteConnection = dbContext.Database.GetDbConnection() as SqliteConnection;
        if (sqliteConnection is null)
        {
            return null;
        }

        var dataSource = sqliteConnection.DataSource;
        if (string.IsNullOrWhiteSpace(dataSource)
            || string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase)
            || dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Path.GetFullPath(dataSource);
    }

    private static DateTimeOffset? ParseBackupTimestampUtc(string backupPath)
    {
        const string prefix = "boardoil-backup-";
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(backupPath);
        if (!fileNameWithoutExtension.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var timestampText = fileNameWithoutExtension[prefix.Length..];
        var formats = new[]
        {
            "yyyy-MM-dd'T'HH-mm-ss'Z'",
            "yyyy-MM-dd'T'HH-mm-ss.fffffff'Z'"
        };
        var parsed = DateTimeOffset.TryParseExact(
            timestampText,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsedTimestamp);
        if (!parsed)
        {
            return null;
        }

        return parsedTimestamp;
    }
}
