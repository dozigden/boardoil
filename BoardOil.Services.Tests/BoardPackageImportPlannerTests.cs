using BoardOil.Contracts.Board;
using BoardOil.Services.Board.Import;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class BoardPackageImportPlannerTests
{
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
        var result = planner.BuildBoardPackageImportPlan("Collision Board", "Collision board description", payload, null);

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
        var result = planner.BuildBoardPackageImportPlan("Bad Style Json Board", "Style json must be object", payload, null);

        Assert.NotNull(result.Error);
        Assert.NotNull(result.Error!.ValidationErrors);
        Assert.Contains(result.Error.ValidationErrors!.Keys, key => key.Contains("stylePropertiesJson", StringComparison.Ordinal));
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
        var result = planner.BuildBoardPackageImportPlan("Canonical Board", "Canonical board description", payload, null);

        Assert.Null(result.Error);
        Assert.NotNull(result.Plan);
        var card = result.Plan!.Columns.Single().Cards.Single();
        Assert.Equal(["Urgent"], card.TagNames);
        Assert.Equal("user@example.com", card.AssignedUserNormalisedEmail);
        Assert.Equal("author@example.com", card.Comments.Single().AuthorNormalisedEmail);
    }
}
