using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Ordering;

namespace BoardOil.Services.Card;

public sealed class CreateCardPlanner
{
    public CreateCardCardTypeSelectionResult SelectCardType(
        int? requestedCardTypeId,
        EntityCardType? requestedCardType,
        EntityCardType? systemCardType)
    {
        if (requestedCardTypeId is not null && requestedCardType is null)
        {
            return new CreateCardCardTypeSelectionResult(null, ApiErrors.ValidationFailed([new ValidationError("cardTypeId", "Card type does not exist in board.")]));
        }

        var selectedCardType = requestedCardType ?? systemCardType;
        if (selectedCardType is null)
        {
            return new CreateCardCardTypeSelectionResult(null, ApiErrors.InternalError("System card type not found for board."));
        }

        return new CreateCardCardTypeSelectionResult(selectedCardType, null);
    }

    public CreateCardDraftResult BuildDraft(CreateCardRequest request, EntityBoardColumn targetColumn, EntityCardType selectedCardType, string? nextSortKey)
    {
        var sortKeyResult = AllocateSortKey(nextSortKey);
        if (sortKeyResult.Error is not null)
        {
            return new CreateCardDraftResult(null, sortKeyResult.Error);
        }

        var title = request.Title.Trim();
        var description = request.Description ?? string.Empty;
        var draft = new CreateCardDraft(
            TargetColumnId: targetColumn.Id,
            CardTypeId: selectedCardType.Id,
            Title: title,
            Description: description,
            AssignedUserId: request.AssignedUserId,
            SortKey: sortKeyResult.SortKey!);
        return new CreateCardDraftResult(draft, null);
    }

    private static SortKeyAllocationResult AllocateSortKey(string? nextSortKey)
    {
        try
        {
            var sortKey = SortKeyGenerator.Between(null, nextSortKey);
            return new SortKeyAllocationResult(sortKey, null);
        }
        catch (InvalidOperationException)
        {
            return new SortKeyAllocationResult(null, ApiErrors.InternalError("Unable to assign card order key."));
        }
        catch (ArgumentException)
        {
            return new SortKeyAllocationResult(null, ApiErrors.InternalError("Unable to assign card order key."));
        }
    }}

public readonly record struct CreateCardCardTypeSelectionResult(EntityCardType? SelectedCardType, ApiError? Error);
public readonly record struct CreateCardDraft(
    int TargetColumnId,
    int CardTypeId,
    string Title,
    string Description,
    int? AssignedUserId,
    string SortKey);
public readonly record struct CreateCardDraftResult(CreateCardDraft? Draft, ApiError? Error);
public readonly record struct SortKeyAllocationResult(string? SortKey, ApiError? Error);
