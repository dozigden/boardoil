using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class ClientPasswordHashMigrationIntegrationTests
{
    [Fact]
    public async Task MakeClientPasswordHashOptionalMigration_ShouldClearOnlyClientPasswordsAndPreserveRelationships()
    {
        // Arrange
        var databasePath = CreateDatabasePath();
        var options = new DbContextOptionsBuilder<BoardOilDbContext>()
            .UseSqlite($"Data Source={databasePath};Pooling=False")
            .Options;

        try
        {
            const int boardId = 1;
            const int clientId = 1;
            const int humanId = 2;
            const int tokenId = 1;

            await using (var dbContext = new BoardOilDbContext(options))
            {
                await dbContext.Database.MigrateAsync("20260731171513_AddBoardScopedCardIdFoundation");

                var timestamp = DateTime.UtcNow;
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO Users
                        (Id, UserName, DisplayName, Email, NormalisedEmail, PasswordHash, Role, IdentityType, IsActive, CreatedAtUtc, UpdatedAtUtc)
                    VALUES
                        ({clientId}, 'existing-client', 'Existing Client', 'existing-client@localhost', 'existing-client@localhost',
                         'legacy-synthetic-password-hash', 0, 1, 1, {timestamp}, {timestamp}),
                        ({humanId}, 'existing-human', 'Existing Human', 'existing-human@localhost', 'existing-human@localhost',
                         'real-human-password-hash', 1, 0, 1, {timestamp}, {timestamp});

                    INSERT INTO Boards (Id, Name, Description, SlickCohesionModeEnabled, CreatedAtUtc, UpdatedAtUtc)
                    VALUES ({boardId}, 'Existing board', 'Migration fixture', 1, {timestamp}, {timestamp});

                    INSERT INTO BoardMembers (BoardId, UserId, Role, CreatedAtUtc, UpdatedAtUtc)
                    VALUES ({boardId}, {clientId}, 0, {timestamp}, {timestamp});

                    INSERT INTO PersonalAccessTokens (Id, UserId, Name, TokenHash, TokenPrefix, ScopesCsv, CreatedAtUtc)
                    VALUES ({tokenId}, {clientId}, 'Existing PAT', 'EXISTING_TOKEN_HASH', 'bo_pat_EXIST', 'mcp:read', {timestamp});
                    """);
            }

            // Act
            await using (var dbContext = new BoardOilDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
            }

            // Assert
            await using var assertContext = new BoardOilDbContext(options);
            var migratedClient = await assertContext.Users.SingleAsync(x => x.Id == clientId);
            var migratedHuman = await assertContext.Users.SingleAsync(x => x.Id == humanId);
            var migratedMembership = await assertContext.BoardMembers.SingleAsync(
                x => x.BoardId == boardId && x.UserId == clientId);
            var migratedToken = await assertContext.PersonalAccessTokens.SingleAsync(x => x.Id == tokenId);

            Assert.Null(migratedClient.PasswordHash);
            Assert.Equal("real-human-password-hash", migratedHuman.PasswordHash);
            Assert.Equal(BoardMemberRole.Contributor, migratedMembership.Role);
            Assert.Equal(clientId, migratedToken.UserId);
            Assert.Equal("EXISTING_TOKEN_HASH", migratedToken.TokenHash);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    private static string CreateDatabasePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "boardoil-client-password-migration-tests");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{Guid.NewGuid():N}.db");
    }
}
