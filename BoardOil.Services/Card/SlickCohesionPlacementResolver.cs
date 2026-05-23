using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Services.Card;

public sealed class SlickCohesionPlacementResolver
{
    public int? ResolveEffectivePositionAfterCardId(
        bool cohesionEnabled,
        bool isCrossColumnMove,
        int? requestedPositionAfterCardId,
        IReadOnlyList<EntityBoardCard> targetCards,
        IReadOnlyList<EntityBoardCard> movingCards)
    {
        if (!cohesionEnabled || !isCrossColumnMove)
        {
            return requestedPositionAfterCardId;
        }

        if (!TryResolveSharedSlickId(movingCards, out var sharedSlickId))
        {
            return requestedPositionAfterCardId;
        }

        var matchingIndexes = targetCards
            .Select((card, index) => (Card: card, Index: index))
            .Where(x => x.Card.SlickId == sharedSlickId)
            .Select(x => x.Index)
            .ToList();
        if (matchingIndexes.Count == 0)
        {
            return requestedPositionAfterCardId;
        }

        if (requestedPositionAfterCardId is null)
        {
            var firstMatchingIndex = matchingIndexes[0];
            return ResolvePositionAfterCardIdForInsertionIndex(targetCards, firstMatchingIndex);
        }

        var insertionIndex = ResolveInsertionIndex(targetCards, requestedPositionAfterCardId.Value);
        if (insertionIndex is null)
        {
            return requestedPositionAfterCardId;
        }

        var snappedInsertionIndex = ResolveNearestSlickInsertionIndex(matchingIndexes, insertionIndex.Value);
        return ResolvePositionAfterCardIdForInsertionIndex(targetCards, snappedInsertionIndex);
    }

    private static bool TryResolveSharedSlickId(IReadOnlyList<EntityBoardCard> movingCards, out int sharedSlickId)
    {
        sharedSlickId = 0;
        if (movingCards.Count == 0)
        {
            return false;
        }

        var firstSlickId = movingCards[0].SlickId;
        if (firstSlickId is null)
        {
            return false;
        }

        for (var i = 1; i < movingCards.Count; i++)
        {
            if (movingCards[i].SlickId != firstSlickId)
            {
                return false;
            }
        }

        sharedSlickId = firstSlickId.Value;
        return true;
    }

    private static int? ResolveInsertionIndex(IReadOnlyList<EntityBoardCard> targetCards, int positionAfterCardId)
    {
        for (var i = 0; i < targetCards.Count; i++)
        {
            if (targetCards[i].Id == positionAfterCardId)
            {
                return i + 1;
            }
        }

        return null;
    }

    private static int ResolveNearestSlickInsertionIndex(IReadOnlyList<int> matchingIndexes, int insertionIndex)
    {
        var nearestIndex = matchingIndexes[0];
        var nearestDistance = ResolveDistanceToInsertionPoint(nearestIndex, insertionIndex);
        for (var i = 1; i < matchingIndexes.Count; i++)
        {
            var candidateIndex = matchingIndexes[i];
            var candidateDistance = ResolveDistanceToInsertionPoint(candidateIndex, insertionIndex);
            if (candidateDistance < nearestDistance)
            {
                nearestDistance = candidateDistance;
                nearestIndex = candidateIndex;
            }
        }

        return nearestIndex < insertionIndex
            ? nearestIndex + 1
            : nearestIndex;
    }

    private static int ResolveDistanceToInsertionPoint(int cardIndex, int insertionIndex)
    {
        // Compare against card center in doubled integer space to avoid floating-point math.
        var doubledCardCenter = (cardIndex * 2) + 1;
        var doubledInsertionPoint = insertionIndex * 2;
        return Math.Abs(doubledCardCenter - doubledInsertionPoint);
    }

    private static int? ResolvePositionAfterCardIdForInsertionIndex(IReadOnlyList<EntityBoardCard> targetCards, int insertionIndex)
    {
        if (insertionIndex <= 0)
        {
            return null;
        }

        if (insertionIndex > targetCards.Count)
        {
            return targetCards[^1].Id;
        }

        return targetCards[insertionIndex - 1].Id;
    }
}
