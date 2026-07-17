using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Ordering;

namespace BoardOil.Services.Card;

public sealed class CardInsertionOrderPlanner(CardSortKeyRenormaliser renormaliser)
{
    public CardInsertionOrderPlan CreateLeadingPlan(
        EntityBoardCard insertedCard,
        IReadOnlyList<EntityBoardCard> existingCardsInOrder)
    {
        try
        {
            var nextKey = existingCardsInOrder.FirstOrDefault()?.SortKey;
            var sortKey = SortKeyGenerator.Between(null, nextKey);
            return new CardInsertionOrderPlan(
                [new CardSortKeyAssignment(insertedCard, sortKey)],
                false,
                null);
        }
        catch (InvalidOperationException)
        {
            return CreateRenormalisedPlan(insertedCard, existingCardsInOrder);
        }
        catch (ArgumentException)
        {
            return CreateRenormalisedPlan(insertedCard, existingCardsInOrder);
        }
    }

    private CardInsertionOrderPlan CreateRenormalisedPlan(
        EntityBoardCard insertedCard,
        IReadOnlyList<EntityBoardCard> existingCardsInOrder)
    {
        try
        {
            var cardsInFinalOrder = new List<EntityBoardCard>(existingCardsInOrder.Count + 1)
            {
                insertedCard
            };
            cardsInFinalOrder.AddRange(existingCardsInOrder);
            var renormalisationPlan = renormaliser.CreatePlan(cardsInFinalOrder);
            return new CardInsertionOrderPlan(renormalisationPlan.Assignments, true, null);
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

    private static CardInsertionOrderPlan UnableToAssignOrderKey() =>
        new([], false, ApiErrors.InternalError("Unable to assign card order key."));
}

public sealed record CardInsertionOrderPlan(
    IReadOnlyList<CardSortKeyAssignment> Assignments,
    bool Renormalised,
    ApiError? Error);
