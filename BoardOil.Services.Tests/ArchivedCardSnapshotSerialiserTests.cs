using BoardOil.Services.Card;
using BoardOil.Data.Abstractions.Entities;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class ArchivedCardSnapshotSerialiserTests
{
    [Fact]
    public void CreateSnapshotJson_AndTryReadKnownPayload_ShouldRoundTripCurrentVersion()
    {
        // Arrange
        var capturedAtUtc = new DateTime(2026, 4, 19, 16, 0, 0, DateTimeKind.Utc);
        var card = BuildCardEntity();

        // Act
        var snapshotJson = ArchivedCardSnapshotSerialiser.CreateSnapshotJson(99, card, capturedAtUtc);
        var parsed = ArchivedCardSnapshotSerialiser.TryReadKnownPayload(snapshotJson, out var knownPayload, out var error);

        // Assert
        Assert.True(parsed);
        Assert.Null(error);
        Assert.NotNull(knownPayload);
        Assert.Equal(ArchivedCardSnapshotSerialiser.SchemaName, knownPayload!.Schema);
        Assert.Equal(ArchivedCardSnapshotSerialiser.CurrentVersion, knownPayload.Version);
        Assert.Equal(capturedAtUtc, knownPayload.CapturedAtUtc);
        Assert.Equal(99, knownPayload.Payload.BoardId);
        Assert.Equal(card.BoardCardId, knownPayload.Payload.OriginalCardId);
        Assert.Equal(card.Title, knownPayload.Payload.Title);
        Assert.Equal(card.ExternalUrl, knownPayload.Payload.ExternalUrl);
        Assert.Equal(card.AssignedUser!.Email, knownPayload.Payload.AssignedUserEmail);
        Assert.Equal(["Bug"], knownPayload.Payload.TagNames);
        Assert.Equal(card.Slick!.Name, knownPayload.Payload.SlickName);
        Assert.NotNull(knownPayload.Payload.Comments);
        var firstComment = Assert.Single(knownPayload.Payload.Comments!);
        Assert.Equal("Archived note", firstComment.Text);
        Assert.Equal(card.Comments.Single().PostedAtUtc, firstComment.CreatedAtUtc);
        Assert.Equal(11, firstComment.AuthorUserId);
        Assert.Equal("[email protected]", firstComment.AuthorEmail);
    }

    [Fact]
    public void TryReadKnownPayload_WhenVersionIsUnknownNewer_ShouldReturnFalse()
    {
        // Arrange
        const string SnapshotJson = """
            {"schema":"archived-card","version":999,"capturedAtUtc":"2026-04-19T16:00:00Z","payload":{"title":"Future"}}
            """;

        // Act
        var parsed = ArchivedCardSnapshotSerialiser.TryReadKnownPayload(SnapshotJson, out var knownPayload, out var error);

        // Assert
        Assert.False(parsed);
        Assert.Null(knownPayload);
        Assert.Equal("Snapshot version is newer than this runtime supports.", error);
    }

    [Fact]
    public void TryBuildCurrentCardDto_WhenSnapshotIsKnown_ShouldReturnCardDto()
    {
        // Arrange
        var capturedAtUtc = new DateTime(2026, 4, 19, 16, 0, 0, DateTimeKind.Utc);
        var card = BuildCardEntity();
        var snapshotJson = ArchivedCardSnapshotSerialiser.CreateSnapshotJson(99, card, capturedAtUtc);

        // Act
        var parsed = ArchivedCardSnapshotSerialiser.TryBuildCurrentCardDto(snapshotJson, out var parsedCard, out var error);

        // Assert
        Assert.True(parsed);
        Assert.Null(error);
        Assert.NotNull(parsedCard);
        Assert.Equal(card.Id, parsedCard!.Id);
        Assert.Equal(card.Title, parsedCard.Title);
        Assert.Equal(card.Description, parsedCard.Description);
        Assert.Equal(card.ExternalUrl, parsedCard.ExternalUrl);
        Assert.Equal(["Bug"], parsedCard.TagNames);
        Assert.Null(parsedCard.SlickId);
        Assert.Equal(card.Slick!.Name, parsedCard.SlickName);

        var parsedSnapshot = ArchivedCardSnapshotSerialiser.TryBuildCurrentSnapshot(snapshotJson, out var snapshot, out error);
        Assert.True(parsedSnapshot);
        Assert.NotNull(snapshot);
        Assert.Equal(card.BoardColumn.Title, snapshot!.OriginalColumnName);
        Assert.Equal(card.AssignedUser!.Email, snapshot!.AssignedUserEmail);
        var snapshotComment = Assert.Single(snapshot.Comments);
        Assert.Equal("Archived note", snapshotComment.Text);
        Assert.Equal(11, snapshotComment.AuthorUserId);
    }

    private static EntityBoardCard BuildCardEntity()
    {
        var board = new EntityBoard { Id = 99, Name = "BoardOil" };
        var column = new EntityBoardColumn { Id = 7, BoardId = board.Id, Title = "Todo", SortKey = "A", Board = board };
        var cardType = new EntityCardType
        {
            Id = 4,
            BoardId = board.Id,
            Name = "Story",
            Emoji = null,
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#224466","textColorMode":"auto"}""",
            IsSystem = true
        };
        var tag = new EntityTag
        {
            Id = 8,
            BoardId = board.Id,
            Name = "Bug",
            NormalisedName = "BUG",
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#224466","textColorMode":"auto"}"""
        };

        return new EntityBoardCard
        {
            Id = 42,
            BoardId = board.Id,
            BoardCardId = 42,
            BoardColumnId = column.Id,
            BoardColumn = column,
            CardTypeId = cardType.Id,
            CardType = cardType,
            Title = "Archive me",
            Description = "Desc",
            ExternalUrl = "https://github.com/example/repository",
            AssignedUserId = 12,
            AssignedUser = new EntityUser
            {
                Id = 12,
                UserName = "assignee",
                DisplayName = "Assignee",
                Email = "assignee@example.test",
                NormalisedEmail = "assignee@example.test",
                PasswordHash = "hash",
                Role = UserRole.Standard,
                IdentityType = UserIdentityType.User,
                IsActive = true
            },
            SortKey = "B",
            SlickId = 77,
            Slick = new EntitySlick
            {
                Id = 77,
                BoardId = board.Id,
                Name = "Release train",
                NormalisedName = "RELEASE TRAIN",
                StyleName = "solid",
                StylePropertiesJson = """{"backgroundColor":"#224466","textColorMode":"auto"}"""
            },
            Comments =
            [
                new EntityCardComment
                {
                    Id = 99,
                    AuthorUserId = 11,
                    AuthorUser = new EntityUser
                    {
                        Id = 11,
                        UserName = "author",
                        DisplayName = "author",
                        Email = "[email protected]",
                        NormalisedEmail = "[email protected]",
                        PasswordHash = "hash",
                        Role = UserRole.Standard,
                        IdentityType = UserIdentityType.User,
                        IsActive = true
                    },
                    Text = "Archived note",
                    PostedAtUtc = new DateTime(2026, 4, 1, 10, 30, 0, DateTimeKind.Utc),
                }
            ],
            CardTags =
            [
                new EntityCardTag
                {
                    TagId = tag.Id,
                    Tag = tag
                }
            ]
        };
    }
}
