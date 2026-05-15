using BoardOil.Contracts.Contracts;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Services.Ordering;

namespace BoardOil.Services.Card;

public sealed class UpdateCardPlanner
{
    public UpdateCardTypeSelectionResult SelectCardType(EntityCardType? selectedCardType)
    {
        if (selectedCardType is null)
        {
            return new UpdateCardTypeSelectionResult(null, ValidationFail([new ValidationError("cardTypeId", "Card type does not exist in board.")]));
        }

        return new UpdateCardTypeSelectionResult(selectedCardType, null);
    }

    public UpdateCardMovementPlanResult PlanMovement(int currentColumnId, int requestedColumnId, IReadOnlyList<EntityBoardCard> targetCards)
    {
        var movementChanged = requestedColumnId != currentColumnId;
        if (!movementChanged)
        {
            return new UpdateCardMovementPlanResult(false, null, null);
        }

        var nextSortKey = targetCards.Count > 0 ? targetCards[0].SortKey : null;
        var sortKeyResult = AllocateSortKey(nextSortKey);
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

    private static UpdateCardSortKeyAllocationResult AllocateSortKey(string? nextSortKey)
    {
        try
        {
            var sortKey = SortKeyGenerator.Between(null, nextSortKey);
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
    }

    private static ApiError ValidationFail(IReadOnlyList<ValidationError> validationErrors) =>
        ApiErrors.BadRequest("Validation failed.", validationErrors);
}

public readonly record struct UpdateCardTypeSelectionResult(EntityCardType? SelectedCardType, ApiError? Error);
public readonly record struct UpdateCardMovementPlanResult(bool MovementChanged, string? TargetSortKey, ApiError? Error);
public readonly record struct UpdateCardSortKeyAllocationResult(string? SortKey, ApiError? Error);
