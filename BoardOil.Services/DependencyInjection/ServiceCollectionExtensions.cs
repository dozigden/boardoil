using BoardOil.Ef;
using BoardOil.Ef.Context;
using BoardOil.Ef.Repositories;
using BoardOil.Ef.Scope;
using BoardOil.Abstractions;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Auth;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.CardType;
using BoardOil.Abstractions.Column;
using BoardOil.Abstractions.Tag;
using BoardOil.Abstractions.Users;
using BoardOil.Persistence.Abstractions.Auth;
using BoardOil.Persistence.Abstractions.Configuration;
using BoardOil.Persistence.Abstractions.Card;
using BoardOil.Persistence.Abstractions.CardType;
using BoardOil.Persistence.Abstractions.Board;
using BoardOil.Persistence.Abstractions.Column;
using BoardOil.Persistence.Abstractions.Tag;
using BoardOil.Persistence.Abstractions.Users;
using BoardOil.Services.Auth;
using BoardOil.Services.Board;
using BoardOil.Services.Card;
using BoardOil.Services.CardType;
using BoardOil.Services.Column;
using BoardOil.Services.Tag;
using BoardOil.Services.Users;
using BoardOil.TasksMd;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace BoardOil.Services.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBoardOilServices(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IDbContextFactory>(_ => new BoardOilDbContextFactory(connectionString));
        services.AddTransient<IDbContextScopeFactory, DbContextScopeFactory>();
        services.AddTransient<IAmbientDbContextLocator, AmbientDbContextLocator>();
        services.AddScoped<IColumnValidator, ColumnValidator>();
        services.AddScoped<ICardValidator, CardValidator>();
        services.AddSingleton<IPasswordHashService, PasswordHashService>();
        services.AddScoped<IBoardRepository, BoardRepository>();
        services.AddScoped<IBoardMemberRepository, BoardMemberRepository>();
        services.AddScoped<IAuthUserRepository, AuthUserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPersonalAccessTokenRepository, PersonalAccessTokenRepository>();
        services.AddScoped<IAppSettingRepository, AppSettingRepository>();
        services.AddScoped<IColumnRepository, ColumnRepository>();
        services.AddScoped<ICardRepository, CardRepository>();
        services.AddScoped<ICardTypeRepository, CardTypeRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IBoardBootstrapService, BoardBootstrapService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBoardService, BoardService>();
        services.AddScoped<IBoardExportService, BoardExportService>();
        services.AddScoped<IBoardPackageImportService, BoardPackageImportService>();
        services.AddScoped<IBoardTasksMdImportService, BoardTasksMdImportService>();
        services.AddScoped<ISystemBoardService, SystemBoardService>();
        services.AddScoped<IBoardAuthorisationService, BoardAuthorisationService>();
        services.AddScoped<IBoardMemberService, BoardMemberService>();
        services.AddScoped<IColumnService, ColumnService>();
        services.AddScoped<ICardService, CardService>();
        services.AddScoped<ICardTypeService, CardTypeService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IUserService, UserService>();
        services.AddHttpClient<ITasksMdClient, TasksMdClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        return services;
    }

    public static async Task InitializeBoardOilAsync(this IServiceProvider serviceProvider)
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
                "PasswordHash" TEXT NOT NULL,
                "Role" INTEGER NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_UserName" ON "Users" ("UserName");
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
