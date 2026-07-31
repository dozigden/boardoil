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
    public async Task PersistBoardPackageImportAsync_With128Columns_ShouldPreserveOrderWithEvenlySpacedKeys()
    {
        // Arrange
        var columns = Enumerable.Range(0, 128)
            .Select(index => new ColumnImportDefinition($"Column {index:D3}", []))
            .ToList();
        var expectedTitles = columns.Select(column => column.Title).ToList();
        var expectedKeys = BoardOil.Abstractions.Ordering.SortKeyGenerator.CreateEvenlySpaced(columns.Count);
        var writer = ResolveService<BoardPackageImportWriter>();

        // Act
        var result = await writer.PersistBoardPackageImportAsync(
            CreatePlan("Large Column Board", columns),
            ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(expectedTitles, result.Data!.Columns.Select(column => column.Title));
        Assert.Equal(expectedKeys, result.Data.Columns.Select(column => column.SortKey));
        Assert.Equal(128, result.Data.Columns.Select(column => column.SortKey).Distinct(StringComparer.Ordinal).Count());
        Assert.All(result.Data.Columns, column => Assert.Equal(20, column.SortKey.Length));
        var sequence = DbContextForAssert.BoardCardIdSequences.Single(x => x.BoardId == result.Data.Id);
        Assert.Equal(1, sequence.NextCardId);
    }

    [Fact]
    public async Task PersistBoardPackageImportAsync_With128CardsInColumn_ShouldPreserveOrderWithEvenlySpacedKeys()
    {
        // Arrange
        var cards = Enumerable.Range(0, 128)
            .Select(CreateCardDefinition)
            .ToList();
        var expectedTitles = cards.Select(card => card.Title).ToList();
        var expectedKeys = BoardOil.Abstractions.Ordering.SortKeyGenerator.CreateEvenlySpaced(cards.Count);
        var writer = ResolveService<BoardPackageImportWriter>();

        // Act
        var result = await writer.PersistBoardPackageImportAsync(
            CreatePlan(
                "Large Card Board",
                [new ColumnImportDefinition("Todo", cards)]),
            ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var importedColumn = Assert.Single(result.Data!.Columns);
        Assert.Equal(expectedTitles, importedColumn.Cards.Select(card => card.Title));
        Assert.Equal(expectedKeys, importedColumn.Cards.Select(card => card.SortKey));
        Assert.Equal(128, importedColumn.Cards.Select(card => card.SortKey).Distinct(StringComparer.Ordinal).Count());
        Assert.All(importedColumn.Cards, card => Assert.Equal(20, card.SortKey.Length));
    }

    [Fact]
    public async Task PersistBoardPackageImportAsync_ShouldPreservePlannedArchiveIdAcrossBoards()
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
        Assert.Equal(777, archivedCard.OriginalCardId);
        var sequence = DbContextForAssert.BoardCardIdSequences.Single(x => x.BoardId == boardId);
        Assert.Equal(778, sequence.NextCardId);
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
                                ],
                                BoardCardId: 1)
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
        var resolvedColumns = columns ?? [];
        var resolvedArchivedCards = archivedCards ?? [];
        var assignedIds = resolvedColumns
            .SelectMany(x => x.Cards)
            .Select(x => x.BoardCardId)
            .Concat(resolvedArchivedCards.Select(x => x.OriginalCardId))
            .Where(x => x > 0)
            .ToList();
        var nextCardId = assignedIds.Count == 0 ? 1 : assignedIds.Max() + 1;
        return new BoardPackageImportPlan(
            boardName,
            $"{boardName} description",
            true,
            nextCardId,
            CardTypeDefaults.SystemTypeName,
            BoardPackageImportNormalisation.NormaliseName(CardTypeDefaults.SystemTypeName),
            null,
            CardTypeDefaults.DefaultStyleName,
            CardTypeDefaults.DefaultStylePropertiesJson,
            [],
            [],
            [],
            resolvedColumns,
            resolvedArchivedCards);
    }

    private static CardImportDefinition CreateCardDefinition(int index) =>
        new(
            $"Card {index:D3}",
            $"Description {index:D3}",
            BoardPackageImportNormalisation.NormaliseName(CardTypeDefaults.SystemTypeName),
            [],
            null,
            null,
            [],
            BoardCardId: index + 1);
}
