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
            int clientId;
            int humanId;
            int tokenId;

            await using (var dbContext = new BoardOilDbContext(options))
            {
                await dbContext.Database.MigrateAsync("20260731171513_AddBoardScopedCardIdFoundation");

                var client = new EntityUser
                {
                    UserName = "existing-client",
                    DisplayName = "Existing Client",
                    Email = "existing-client@localhost",
                    PasswordHash = "legacy-synthetic-password-hash",
                    Role = UserRole.Standard,
                    IdentityType = UserIdentityType.Client,
                    IsActive = true,
                };
                var human = new EntityUser
                {
                    UserName = "existing-human",
                    DisplayName = "Existing Human",
                    Email = "existing-human@localhost",
                    PasswordHash = "real-human-password-hash",
                    Role = UserRole.Admin,
                    IdentityType = UserIdentityType.User,
                    IsActive = true,
                };
                foreach (var user in new[] { client, human })
                {
                    await dbContext.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        INSERT INTO Users (UserName,DisplayName,Email,NormalisedEmail,PasswordHash,Role,IdentityType,IsActive,CreatedAtUtc,UpdatedAtUtc)
                        VALUES ({user.UserName},{user.DisplayName},{user.Email},{user.Email},{user.PasswordHash},{(int)user.Role},{(int)user.IdentityType},1,{DateTime.UtcNow},{DateTime.UtcNow});
                        """);
                    user.Id = await dbContext.Users.Where(x => x.UserName == user.UserName).Select(x => x.Id).SingleAsync();
                }
                var timestamp = DateTime.UtcNow;
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO "Boards"
                        ("Id", "Name", "Description", "SlickCohesionModeEnabled", "CreatedAtUtc", "UpdatedAtUtc")
                    VALUES
                        ({boardId}, {"Existing board"}, {"Migration fixture"}, {true}, {timestamp}, {timestamp});
                    """);

                var membership = new EntityBoardMember
                {
                    BoardId = boardId,
                    UserId = client.Id,
                    Role = BoardMemberRole.Contributor,
                };
                var token = new EntityPersonalAccessToken
                {
                    UserId = client.Id,
                    Name = "Existing PAT",
                    TokenHash = "EXISTING_TOKEN_HASH",
                    TokenPrefix = "bo_pat_EXIST",
                    ScopesCsv = "mcp:read",
                };
                dbContext.BoardMembers.Add(membership);
                dbContext.PersonalAccessTokens.Add(token);
                await dbContext.SaveChangesAsync();

                clientId = client.Id;
                humanId = human.Id;
                tokenId = token.Id;
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
