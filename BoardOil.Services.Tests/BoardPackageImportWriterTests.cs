using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Board.Import;
using BoardOil.Services.Card;
using BoardOil.Services.Tests.Infrastructure;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class BoardPackageImportWriterTests : TestBaseDb
{
    [Fact]
    public async Task PersistBoardPackageImportAsync_WhenArchiveOriginalCardIdCollides_ShouldAssignFallbackId()
    {
        var existingBoard = CreateBoard("Existing Archive Board")
            .AddColumn("Todo")
            .Build();

        DbContextForArrange.ArchivedCards.Add(new EntityArchivedCard
        {
            BoardId = existingBoard.BoardId,
            OriginalCardId = 777,
            ArchivedAtUtc = new DateTime(2026, 04, 20, 9, 0, 0, DateTimeKind.Utc),
            SnapshotJson = """{"schema":"archived-card","version":1,"capturedAtUtc":"2026-04-20T09:00:00Z","payload":{"title":"Existing"}}""",
            SearchTitle = "Existing",
            SearchTagsJson = "[]",
            SearchTextNormalised = "EXISTING"
        });
        await DbContextForArrange.SaveChangesAsync();

        var writer = ResolveService<BoardPackageImportWriter>();
        var result = await writer.PersistBoardPackageImportAsync(
            CreatePlan(
                boardName: "Archive Collision Board",
                archivedCards:
                [
                    new ArchivedCardImportDefinition(
                        777,
                        "Colliding archived card",
                        [],
                        new DateTime(2026, 04, 20, 11, 0, 0, DateTimeKind.Utc),
                        """{"schema":"archived-card","version":1,"capturedAtUtc":"2026-04-20T11:00:00Z","payload":{"title":"Colliding archived card"}}""")
                ]),
            ActorUserId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var boardId = result.Data!.Id;
        var archivedCard = DbContextForAssert.ArchivedCards.Single(x => x.BoardId == boardId);
        Assert.NotEqual(777, archivedCard.OriginalCardId);
        Assert.True(archivedCard.OriginalCardId < 0);
    }

    [Fact]
    public async Task PersistBoardPackageImportAsync_ShouldMapAssignedUsersAndCommentAuthorsByEmail()
    {
        var activeUser = DbContextForArrange.Users.Single(x => x.Id == ActorUserId);
        var inactiveUserEmail = $"inactive-{Guid.NewGuid():N}@example.com";
        var inactiveUser = new EntityUser
        {
            UserName = $"inactive-{Guid.NewGuid():N}",
            Email = inactiveUserEmail,
            NormalisedEmail = inactiveUserEmail.ToLowerInvariant(),
            PasswordHash = "test-hash",
            Role = UserRole.Standard,
            IdentityType = UserIdentityType.User,
            IsActive = false,
        };
        DbContextForArrange.Users.Add(inactiveUser);
        await DbContextForArrange.SaveChangesAsync();

        var writer = ResolveService<BoardPackageImportWriter>();
        var result = await writer.PersistBoardPackageImportAsync(
            CreatePlan(
                boardName: "User Mapping Board",
                columns:
                [
                    new ColumnImportDefinition(
                        "Todo",
                        [
                            new CardImportDefinition(
                                "Assigned card",
                                "Description",
                                BoardPackageImportNormalisation.NormaliseName(CardTypeDefaults.SystemTypeName),
                                [],
                                null,
                                activeUser.NormalisedEmail,
                                [
                                    new CommentImportDefinition("Active author", new DateTime(2026, 05, 02, 8, 0, 0, DateTimeKind.Utc), activeUser.NormalisedEmail),
                                    new CommentImportDefinition("Inactive author", new DateTime(2026, 05, 02, 8, 1, 0, DateTimeKind.Utc), inactiveUser.NormalisedEmail)
                                ])
                        ])
                ]),
            ActorUserId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var boardId = result.Data!.Id;
        var importedCard = DbContextForAssert.Cards.Single(x => x.BoardColumn.BoardId == boardId);
        Assert.Equal(activeUser.Id, importedCard.AssignedUserId);

        var importedComments = DbContextForAssert.CardComments
            .Where(x => x.CardId == importedCard.Id)
            .OrderBy(x => x.PostedAtUtc)
            .ToList();

        Assert.Equal(2, importedComments.Count);
        Assert.Equal(activeUser.Id, importedComments[0].AuthorUserId);
        Assert.Equal(inactiveUser.Id, importedComments[1].AuthorUserId);
    }

    private static BoardPackageImportPlan CreatePlan(
        string boardName,
        IReadOnlyList<ColumnImportDefinition>? columns = null,
        IReadOnlyList<ArchivedCardImportDefinition>? archivedCards = null)
    {
        return new BoardPackageImportPlan(
            boardName,
            $"{boardName} description",
            true,
            CardTypeDefaults.SystemTypeName,
            BoardPackageImportNormalisation.NormaliseName(CardTypeDefaults.SystemTypeName),
            null,
            CardTypeDefaults.DefaultStyleName,
            CardTypeDefaults.DefaultStylePropertiesJson,
            [],
            [],
            [],
            columns ?? [],
            archivedCards ?? []);
    }
}
