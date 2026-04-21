using BoardOil.Services.Card;
using BoardOil.Persistence.Abstractions.Entities;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class ArchivedCardSnapshotSerialiserTests
{
    [Fact]
    public void CreateSnapshotJson_AndTryReadKnownPayload_ShouldRoundTripV1()
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
        Assert.Equal(1, knownPayload.Version);
        Assert.Equal(capturedAtUtc, knownPayload.CapturedAtUtc);
        Assert.Equal(99, knownPayload.Payload.BoardId);
        Assert.Equal(card.Id, knownPayload.Payload.OriginalCardId);
        Assert.Equal(card.Title, knownPayload.Payload.Title);
        Assert.Equal(["Bug"], knownPayload.Payload.TagNames);
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
        Assert.Equal(["Bug"], parsedCard.TagNames);
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
            BoardColumnId = column.Id,
            BoardColumn = column,
            CardTypeId = cardType.Id,
            CardType = cardType,
            Title = "Archive me",
            Description = "Desc",
            SortKey = "B",
            CreatedAtUtc = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc),
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
