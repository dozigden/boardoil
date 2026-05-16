using BoardOil.Contracts.Contracts;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Ordering;

namespace BoardOil.Services.Card;

public sealed class MoveCardPlanner
{
    public MoveCardAnchorResolution ResolveAnchor(int? positionAfterCardId, IReadOnlyList<EntityBoardCard> targetCards)
    {
        if (positionAfterCardId is null)
        {
            var firstSortKey = targetCards.Count > 0 ? targetCards[0].SortKey : null;
            return new MoveCardAnchorResolution(null, null, firstSortKey);
        }

        var anchorIndex = FindCardIndex(targetCards, positionAfterCardId.Value);
        if (anchorIndex < 0)
        {
            return new MoveCardAnchorResolution(
                ApiErrors.ValidationFailed([new ValidationError("positionAfterCardId", "Card does not exist in target column.")]),
                null,
                null);
        }

        var previousKey = targetCards[anchorIndex].SortKey;
        var nextKey = anchorIndex < targetCards.Count - 1
            ? targetCards[anchorIndex + 1].SortKey
            : null;
        return new MoveCardAnchorResolution(null, previousKey, nextKey);
    }

    public MoveCardSortKeyResult AllocateSortKey(string? previousKey, string? nextKey)
    {
        try
        {
            return new MoveCardSortKeyResult(SortKeyGenerator.Between(previousKey, nextKey), null);
        }
        catch (InvalidOperationException)
        {
            return new MoveCardSortKeyResult(null, ApiErrors.InternalError("Unable to assign card order key."));
        }
        catch (ArgumentException)
        {
            return new MoveCardSortKeyResult(null, ApiErrors.InternalError("Unable to assign card order key."));
        }
    }

    public int FindCardIndex(IReadOnlyList<EntityBoardCard> cards, int targetId)
    {
        for (var i = 0; i < cards.Count; i++)
        {
            if (cards[i].Id == targetId)
            {
                return i;
            }
        }

        return -1;
    }}

public readonly record struct MoveCardAnchorResolution(ApiError? Error, string? PreviousKey, string? NextKey);
public readonly record struct MoveCardSortKeyResult(string? SortKey, ApiError? Error);
