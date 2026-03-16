using BoardOil.Ef;
using BoardOil.Services.Abstractions;
using Microsoft.Data.Sqlite;
using BoardOil.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BoardOil.Services.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBoardOilServices(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<BoardOilDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IColumnValidator, ColumnValidator>();
        services.AddScoped<ICardValidator, CardValidator>();
        services.AddSingleton<IPasswordHashService, PasswordHashService>();
        services.AddScoped<IBoardRepository, BoardRepository>();
        services.AddScoped<IColumnRepository, ColumnRepository>();
        services.AddScoped<ICardRepository, CardRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IBoardBootstrapService, BoardBootstrapService>();
        services.AddScoped<IBoardService, BoardService>();
        services.AddScoped<IColumnService, ColumnService>();
        services.AddScoped<ICardService, CardService>();
        services.AddScoped<IUserAdminService, UserAdminService>();

        return services;
    }

    public static async Task InitializeBoardOilAsync(this IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BoardOilDbContext>();
        var hasMigrations = dbContext.Database.GetMigrations().Any();
        if (hasMigrations)
        {
            await EnsureLegacyEnsureCreatedDatabaseCanMigrateAsync(dbContext);
            await dbContext.Database.MigrateAsync();
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
}
