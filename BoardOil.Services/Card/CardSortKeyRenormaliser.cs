using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Ordering;

namespace BoardOil.Services.Card;

public sealed class CardSortKeyRenormaliser
{
    public CardSortKeyRenormalisationPlan CreatePlan(IReadOnlyList<EntityBoardCard> cardsInFinalOrder)
    {
        if (cardsInFinalOrder.Count == 0)
        {
            return new CardSortKeyRenormalisationPlan([]);
        }

        var occupiedKeys = cardsInFinalOrder
            .Select(card => card.SortKey)
            .ToHashSet(StringComparer.Ordinal);
        var replacementKeys = SortKeyGenerator.CreateEvenlySpaced(cardsInFinalOrder.Count, occupiedKeys);
        var assignments = new List<CardSortKeyAssignment>(cardsInFinalOrder.Count);
        for (var index = 0; index < cardsInFinalOrder.Count; index++)
        {
            assignments.Add(new CardSortKeyAssignment(cardsInFinalOrder[index], replacementKeys[index]));
        }

        return new CardSortKeyRenormalisationPlan(assignments);
    }
}

public sealed record CardSortKeyRenormalisationPlan(IReadOnlyList<CardSortKeyAssignment> Assignments);
public readonly record struct CardSortKeyAssignment(EntityBoardCard Card, string SortKey);
