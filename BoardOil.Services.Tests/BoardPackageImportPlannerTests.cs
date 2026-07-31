using BoardOil.Contracts.Board;
using BoardOil.Services.Board.Import;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class BoardPackageImportPlannerTests
{
    [Fact]
    public void BuildBoardPackageImportPlan_WhenSchemaThreeIdentityFieldsAreMissing_ShouldReturnValidationErrors()
    {
        var payload = new BoardPackageBoardDto(
            "Missing Identity Board",
            "Missing identity board description",
            [new BoardPackageCardTypeDto("Story", null, true)],
            [],
            [
                new BoardPackageColumnDto(
                    "Todo",
                    [new BoardPackageCardDto("Active", "Description", "Story", [])])
            ]);

        var planner = new BoardPackageImportPlanner();
        var result = planner.BuildBoardPackageImportPlan(
            "Missing Identity Board",
            "Missing identity board description",
            true,
            payload,
            null,
            schemaVersion: 3);

        Assert.NotNull(result.Error);
        Assert.Contains("board.columns[0].cards[0].id", result.Error!.ValidationErrors!.Keys);
        Assert.Contains("board.nextCardId", result.Error.ValidationErrors.Keys);
    }

    [Fact]
    public void BuildBoardPackageImportPlan_WhenSchemaThreeCardIdIsSharedByLiveAndArchivedCard_ShouldReturnValidationError()
    {
        var payload = new BoardPackageBoardDto(
            "Duplicate Identity Board",
            "Duplicate identity board description",
            [new BoardPackageCardTypeDto("Story", null, true)],
            [],
            [
                new BoardPackageColumnDto(
                    "Todo",
                    [new BoardPackageCardDto("Active", "Description", "Story", [], Id: 5)])
            ],
            NextCardId: 6);
        var archivePayload = new BoardPackageArchiveDto(
            [
                new BoardPackageArchivedCardDto(
                    5,
                    "Archived",
                    [],
                    new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc),
                    "{}")
            ]);

        var planner = new BoardPackageImportPlanner();
        var result = planner.BuildBoardPackageImportPlan(
            "Duplicate Identity Board",
            "Duplicate identity board description",
            true,
            payload,
            archivePayload,
            schemaVersion: 3);

        Assert.NotNull(result.Error);
        Assert.Contains("archive.cards[0].originalCardId", result.Error!.ValidationErrors!.Keys);
    }

    [Fact]
    public void BuildBoardPackageImportPlan_WhenSchemaThreeNextCardIdDoesNotExceedHighWaterMark_ShouldReturnValidationError()
    {
        var payload = new BoardPackageBoardDto(
            "Invalid Sequence Board",
            "Invalid sequence board description",
            [new BoardPackageCardTypeDto("Story", null, true)],
            [],
            [
                new BoardPackageColumnDto(
                    "Todo",
                    [new BoardPackageCardDto("Active", "Description", "Story", [], Id: 8)])
            ],
            NextCardId: 8);

        var planner = new BoardPackageImportPlanner();
        var result = planner.BuildBoardPackageImportPlan(
            "Invalid Sequence Board",
            "Invalid sequence board description",
            true,
            payload,
            null,
            schemaVersion: 3);

        Assert.NotNull(result.Error);
        Assert.Contains("board.nextCardId", result.Error!.ValidationErrors!.Keys);
    }

    [Fact]
    public void BuildBoardPackageImportPlan_WhenTagNamesCollideByCase_ShouldReturnValidationError()
    {
        var payload = new BoardPackageBoardDto(
            "Collision Board",
            "Collision board description",
            [new BoardPackageCardTypeDto("Story", null, true)],
            [
                new BoardPackageTagDto("Urgent", "solid", """{"backgroundColor":"#ED333B","textColorMode":"auto"}""", null),
                new BoardPackageTagDto("urgent", "solid", """{"backgroundColor":"#224466","textColorMode":"auto"}""", null)
            ],
            [new BoardPackageColumnDto("Todo", [])]);

        var planner = new BoardPackageImportPlanner();
        var result = planner.BuildBoardPackageImportPlan("Collision Board", "Collision board description", true, payload, null);

        Assert.NotNull(result.Error);
        Assert.NotNull(result.Error!.ValidationErrors);
        Assert.Contains("board.tags[1].name", result.Error.ValidationErrors!.Keys);
    }

    [Fact]
    public void BuildBoardPackageImportPlan_WhenStyleJsonIsNotObject_ShouldReturnValidationError()
    {
        var payload = new BoardPackageBoardDto(
            "Bad Style Json Board",
            "Style json must be object",
            [new BoardPackageCardTypeDto("Story", null, true, "solid", """["bad"]""")],
            [],
            [new BoardPackageColumnDto("Todo", [])]);

        var planner = new BoardPackageImportPlanner();
        var result = planner.BuildBoardPackageImportPlan("Bad Style Json Board", "Style json must be object", true, payload, null);

        Assert.NotNull(result.Error);
        Assert.NotNull(result.Error!.ValidationErrors);
        Assert.Contains(result.Error.ValidationErrors!.Keys, key => key.Contains("stylePropertiesJson", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildBoardPackageImportPlan_WhenCardExternalUrlIsNotHttpOrHttps_ShouldReturnValidationError()
    {
        var payload = new BoardPackageBoardDto(
            "Bad External URL Board",
            "External URL must be valid",
            [new BoardPackageCardTypeDto("Story", null, true)],
            [],
            [
                new BoardPackageColumnDto(
                    "Todo",
                    [
                        new BoardPackageCardDto(
                            "Card",
                            "Description",
                            "Story",
                            [],
                            ExternalUrl: "ftp://example.com/file")
                    ])
            ]);

        var planner = new BoardPackageImportPlanner();
        var result = planner.BuildBoardPackageImportPlan(
            "Bad External URL Board",
            "External URL must be valid",
            true,
            payload,
            null);

        Assert.NotNull(result.Error);
        Assert.NotNull(result.Error!.ValidationErrors);
        Assert.Contains("board.columns[0].cards[0].externalUrl", result.Error.ValidationErrors!.Keys);
    }

    [Fact]
    public void BuildBoardPackageImportPlan_ShouldCanonicaliseCardTagNamesAndEmails()
    {
        var payload = new BoardPackageBoardDto(
            "Canonical Board",
            "Canonical board description",
            [new BoardPackageCardTypeDto("Story", null, true)],
            [new BoardPackageTagDto("Urgent", "solid", """{"backgroundColor":"#ED333B","textColorMode":"auto"}""", null)],
            [
                new BoardPackageColumnDto(
                    "Todo",
                    [
                        new BoardPackageCardDto(
                            "Card",
                            "Description",
                            "Story",
                            ["Urgent", "urgent"],
                            "USER@EXAMPLE.COM",
                            [new BoardPackageCommentDto("Hello", new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc), "AUTHOR@EXAMPLE.COM")])
                    ])
            ]);

        var planner = new BoardPackageImportPlanner();
        var result = planner.BuildBoardPackageImportPlan("Canonical Board", "Canonical board description", true, payload, null);

        Assert.Null(result.Error);
        Assert.NotNull(result.Plan);
        var card = result.Plan!.Columns.Single().Cards.Single();
        Assert.Equal(["Urgent"], card.TagNames);
        Assert.Equal("user@example.com", card.AssignedUserNormalisedEmail);
        Assert.Equal("author@example.com", card.Comments.Single().AuthorNormalisedEmail);
    }
}
