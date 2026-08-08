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
            int clientId;
            int humanId;
            int boardId;
            int tokenId;

            await using (var dbContext = new BoardOilDbContext(options))
            {
                await dbContext.Database.MigrateAsync("20260731171513_AddBoardScopedCardIdFoundation");

                var client = new EntityUser
                {
                    UserName = "existing-client",
                    DisplayName = "Existing Client",
                    Email = "existing-client@localhost",
                    NormalisedEmail = "existing-client@localhost",
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
                    NormalisedEmail = "existing-human@localhost",
                    PasswordHash = "real-human-password-hash",
                    Role = UserRole.Admin,
                    IdentityType = UserIdentityType.User,
                    IsActive = true,
                };
                var board = new EntityBoard
                {
                    Name = "Existing board",
                    Description = "Migration fixture",
                };

                dbContext.Users.AddRange(client, human);
                dbContext.Boards.Add(board);
                await dbContext.SaveChangesAsync();

                var membership = new EntityBoardMember
                {
                    BoardId = board.Id,
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
                boardId = board.Id;
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
