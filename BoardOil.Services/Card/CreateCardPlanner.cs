using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Entities;

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

    public CreateCardDraft BuildDraft(
        CreateCardRequest request,
        EntityBoardColumn targetColumn,
        EntityCardType selectedCardType)
    {
        var title = request.Title.Trim();
        var description = request.Description ?? string.Empty;
        var externalUrl = CardExternalUrl.Normalise(request.ExternalUrl);
        return new CreateCardDraft(
            TargetColumnId: targetColumn.Id,
            CardTypeId: selectedCardType.Id,
            Title: title,
            Description: description,
            AssignedUserId: request.AssignedUserId,
            ExternalUrl: externalUrl);
    }
}

public readonly record struct CreateCardCardTypeSelectionResult(EntityCardType? SelectedCardType, ApiError? Error);
public readonly record struct CreateCardDraft(
    int TargetColumnId,
    int CardTypeId,
    string Title,
    string Description,
    int? AssignedUserId,
    string? ExternalUrl);
