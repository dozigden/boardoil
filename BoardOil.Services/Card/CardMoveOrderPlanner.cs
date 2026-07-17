using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Ordering;

namespace BoardOil.Services.Card;

public sealed class CardMoveOrderPlanner(CardSortKeyRenormaliser renormaliser)
{
    public CardMoveOrderPlan CreatePlan(
        IReadOnlyList<EntityBoardCard> targetCards,
        IReadOnlyList<EntityBoardCard> movingCards,
        int? positionAfterCardId)
    {
        var insertionIndexResult = ResolveInsertionIndex(targetCards, positionAfterCardId);
        if (insertionIndexResult.Error is not null)
        {
            return new CardMoveOrderPlan([], false, insertionIndexResult.Error);
        }

        var insertionIndex = insertionIndexResult.InsertionIndex;
        var previousKey = insertionIndex > 0 ? targetCards[insertionIndex - 1].SortKey : null;
        var nextKey = insertionIndex < targetCards.Count ? targetCards[insertionIndex].SortKey : null;
        var assignments = TryAllocateMovingCardKeys(movingCards, previousKey, nextKey);
        if (assignments is not null)
        {
            return new CardMoveOrderPlan(assignments, false, null);
        }

        try
        {
            var cardsInFinalOrder = targetCards.ToList();
            cardsInFinalOrder.InsertRange(insertionIndex, movingCards);
            var renormalisationPlan = renormaliser.CreatePlan(cardsInFinalOrder);
            return new CardMoveOrderPlan(renormalisationPlan.Assignments, true, null);
        }
        catch (InvalidOperationException)
        {
            return UnableToAssignOrderKey();
        }
        catch (ArgumentException)
        {
            return UnableToAssignOrderKey();
        }
    }

    public int FindCardIndex(IReadOnlyList<EntityBoardCard> cards, int targetId)
    {
        for (var index = 0; index < cards.Count; index++)
        {
            if (cards[index].Id == targetId)
            {
                return index;
            }
        }

        return -1;
    }

    private static CardMoveInsertionIndexResult ResolveInsertionIndex(
        IReadOnlyList<EntityBoardCard> targetCards,
        int? positionAfterCardId)
    {
        if (positionAfterCardId is null)
        {
            return new CardMoveInsertionIndexResult(0, null);
        }

        for (var index = 0; index < targetCards.Count; index++)
        {
            if (targetCards[index].Id == positionAfterCardId.Value)
            {
                return new CardMoveInsertionIndexResult(index + 1, null);
            }
        }

        return new CardMoveInsertionIndexResult(
            0,
            ApiErrors.ValidationFailed([
                new ValidationError("positionAfterCardId", "Card does not exist in target column.")
            ]));
    }

    private static IReadOnlyList<CardSortKeyAssignment>? TryAllocateMovingCardKeys(
        IReadOnlyList<EntityBoardCard> movingCards,
        string? previousKey,
        string? nextKey)
    {
        try
        {
            var assignments = new List<CardSortKeyAssignment>(movingCards.Count);
            foreach (var movingCard in movingCards)
            {
                var sortKey = SortKeyGenerator.Between(previousKey, nextKey);
                assignments.Add(new CardSortKeyAssignment(movingCard, sortKey));
                previousKey = sortKey;
            }

            return assignments;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static CardMoveOrderPlan UnableToAssignOrderKey() =>
        new([], false, ApiErrors.InternalError("Unable to assign card order key."));
}

public sealed record CardMoveOrderPlan(
    IReadOnlyList<CardSortKeyAssignment> Assignments,
    bool Renormalised,
    ApiError? Error);

public readonly record struct CardMoveInsertionIndexResult(int InsertionIndex, ApiError? Error);
