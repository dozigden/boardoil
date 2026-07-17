using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Card;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardSortKeyRenormaliserTests
{
    private const string PreferredRangeStart = "90000000000000000000";
    private const string PreferredRangeEnd = "R0000000000000000000";

    [Fact]
    public void CreatePlan_WhenCardsProvided_ShouldPreserveOrderAndAvoidOccupiedKeys()
    {
        // Arrange
        var initialKeys = BoardOil.Abstractions.Ordering.SortKeyGenerator.CreateEvenlySpaced(3);
        var cards = new[]
        {
            new EntityBoardCard { Id = 1, SortKey = initialKeys[0] },
            new EntityBoardCard { Id = 2, SortKey = initialKeys[1] },
            new EntityBoardCard { Id = 3, SortKey = initialKeys[2] }
        };
        var occupiedKeys = cards.Select(card => card.SortKey).ToHashSet(StringComparer.Ordinal);
        var renormaliser = new CardSortKeyRenormaliser();

        // Act
        var plan = renormaliser.CreatePlan(cards);

        // Assert
        Assert.Equal(cards, plan.Assignments.Select(assignment => assignment.Card));
        Assert.All(plan.Assignments, assignment => Assert.Equal(20, assignment.SortKey.Length));
        Assert.All(plan.Assignments, assignment => Assert.DoesNotContain(assignment.SortKey, occupiedKeys));
        Assert.Equal(
            plan.Assignments.Select(assignment => assignment.SortKey).OrderBy(key => key, StringComparer.Ordinal),
            plan.Assignments.Select(assignment => assignment.SortKey));
        Assert.Equal(cards.Length, plan.Assignments.Select(assignment => assignment.SortKey).Distinct().Count());
    }

    [Fact]
    public void CreatePlan_WhenCalledRepeatedly_ShouldBeDeterministic()
    {
        // Arrange
        var cards = new[]
        {
            new EntityBoardCard { Id = 1, SortKey = "00000000000000000000" },
            new EntityBoardCard { Id = 2, SortKey = "00000000000000000001" }
        };
        var renormaliser = new CardSortKeyRenormaliser();

        // Act
        var firstPlan = renormaliser.CreatePlan(cards);
        var secondPlan = renormaliser.CreatePlan(cards);

        // Assert
        Assert.Equal(
            firstPlan.Assignments.Select(assignment => assignment.SortKey),
            secondPlan.Assignments.Select(assignment => assignment.SortKey));
    }

    [Fact]
    public void CreatePlan_WhenCardsProvided_ShouldReserveOuterQuartersOfKeySpace()
    {
        // Arrange
        var cards = Enumerable.Range(1, 10)
            .Select(id => new EntityBoardCard
            {
                Id = id,
                SortKey = id.ToString("D20")
            })
            .ToArray();
        var renormaliser = new CardSortKeyRenormaliser();

        // Act
        var plan = renormaliser.CreatePlan(cards);

        // Assert
        Assert.All(plan.Assignments, assignment =>
        {
            Assert.True(
                string.CompareOrdinal(assignment.SortKey, PreferredRangeStart) > 0,
                $"Expected '{assignment.SortKey}' to be after '{PreferredRangeStart}'.");
            Assert.True(
                string.CompareOrdinal(assignment.SortKey, PreferredRangeEnd) < 0,
                $"Expected '{assignment.SortKey}' to be before '{PreferredRangeEnd}'.");
        });
    }

    [Fact]
    public void CreatePlan_WhenNoCardsProvided_ShouldReturnNoAssignments()
    {
        // Arrange
        var renormaliser = new CardSortKeyRenormaliser();

        // Act
        var plan = renormaliser.CreatePlan([]);

        // Assert
        Assert.Empty(plan.Assignments);
    }

    [Fact]
    public void CreateEvenlySpaced_WhenCountIsNegative_ShouldRejectCount()
    {
        // Arrange
        const int invalidCount = -1;

        // Act
        var exception = Record.Exception(() =>
            BoardOil.Abstractions.Ordering.SortKeyGenerator.CreateEvenlySpaced(invalidCount));

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }
}
