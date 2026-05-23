using BoardOil.Contracts.Contracts;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Ordering;

namespace BoardOil.Services.Card;

public sealed class UpdateCardPlanner
{
    public UpdateCardTypeSelectionResult SelectCardType(EntityCardType? selectedCardType)
    {
        if (selectedCardType is null)
        {
            return new UpdateCardTypeSelectionResult(null, ApiErrors.ValidationFailed([new ValidationError("cardTypeId", "Card type does not exist in board.")]));
        }

        return new UpdateCardTypeSelectionResult(selectedCardType, null);
    }

    public UpdateCardMovementPlanResult PlanMovement(
        int currentColumnId,
        int requestedColumnId,
        IReadOnlyList<EntityBoardCard> targetCards,
        int? positionAfterCardId)
    {
        var movementChanged = requestedColumnId != currentColumnId;
        if (!movementChanged)
        {
            return new UpdateCardMovementPlanResult(false, null, null);
        }

        var anchorResolution = ResolveAnchor(positionAfterCardId, targetCards);
        if (anchorResolution.Error is not null)
        {
            return new UpdateCardMovementPlanResult(false, null, anchorResolution.Error);
        }

        var sortKeyResult = AllocateSortKey(anchorResolution.PreviousKey, anchorResolution.NextKey);
        if (sortKeyResult.Error is not null)
        {
            return new UpdateCardMovementPlanResult(false, null, sortKeyResult.Error);
        }

        return new UpdateCardMovementPlanResult(true, sortKeyResult.SortKey, null);
    }

    public bool TagsChanged(EntityBoardCard existingCard, IReadOnlyList<EntityTag> updatedTags)
    {
        var updatedTagNames = updatedTags
            .Select(x => x.Name)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        var existingTagNames = existingCard.CardTags
            .Select(x => x.Tag.Name)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        return !existingTagNames.SequenceEqual(updatedTagNames, StringComparer.Ordinal);
    }

    private static UpdateCardAnchorResolution ResolveAnchor(int? positionAfterCardId, IReadOnlyList<EntityBoardCard> targetCards)
    {
        if (positionAfterCardId is null)
        {
            var firstSortKey = targetCards.Count > 0 ? targetCards[0].SortKey : null;
            return new UpdateCardAnchorResolution(null, null, firstSortKey);
        }

        var anchorIndex = FindCardIndex(targetCards, positionAfterCardId.Value);
        if (anchorIndex < 0)
        {
            return new UpdateCardAnchorResolution(
                ApiErrors.ValidationFailed([new ValidationError("positionAfterCardId", "Card does not exist in target column.")]),
                null,
                null);
        }

        var previousKey = targetCards[anchorIndex].SortKey;
        var nextKey = anchorIndex < targetCards.Count - 1
            ? targetCards[anchorIndex + 1].SortKey
            : null;
        return new UpdateCardAnchorResolution(null, previousKey, nextKey);
    }

    private static int FindCardIndex(IReadOnlyList<EntityBoardCard> cards, int targetId)
    {
        for (var i = 0; i < cards.Count; i++)
        {
            if (cards[i].Id == targetId)
            {
                return i;
            }
        }

        return -1;
    }

    private static UpdateCardSortKeyAllocationResult AllocateSortKey(string? previousKey, string? nextKey)
    {
        try
        {
            var sortKey = SortKeyGenerator.Between(previousKey, nextKey);
            return new UpdateCardSortKeyAllocationResult(sortKey, null);
        }
        catch (InvalidOperationException)
        {
            return new UpdateCardSortKeyAllocationResult(null, ApiErrors.InternalError("Unable to assign card order key."));
        }
        catch (ArgumentException)
        {
            return new UpdateCardSortKeyAllocationResult(null, ApiErrors.InternalError("Unable to assign card order key."));
        }
    }}

public readonly record struct UpdateCardTypeSelectionResult(EntityCardType? SelectedCardType, ApiError? Error);
public readonly record struct UpdateCardMovementPlanResult(bool MovementChanged, string? TargetSortKey, ApiError? Error);
public readonly record struct UpdateCardAnchorResolution(ApiError? Error, string? PreviousKey, string? NextKey);
public readonly record struct UpdateCardSortKeyAllocationResult(string? SortKey, ApiError? Error);
