using System.Data;
using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Data.Abstractions.Card;
using BoardOil.Data.Abstractions.CardType;
using BoardOil.Data.Abstractions.Column;
using BoardOil.Data.Abstractions.Image;
using BoardOil.Data.Abstractions.Slick;
using BoardOil.Data.Abstractions.Tag;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Services.Card;

public sealed class TransferCardService(
    IBoardRepository boardRepository,
    IBoardMemberRepository boardMemberRepository,
    ICardRepository cardRepository,
    IBoardCardIdAllocator boardCardIdAllocator,
    ICardTypeRepository cardTypeRepository,
    IColumnRepository columnRepository,
    ITagRepository tagRepository,
    ISlickRepository slickRepository,
    IImageRepository imageRepository,
    IBoardAuthorisationService boardAuthorisationService,
    CardTransferContentPlanner contentPlanner,
    CardInsertionOrderPlanner insertionOrderPlanner,
    IBoardEvents boardEvents,
    IDbContextScopeFactory scopeFactory)
{
    public async Task<ApiResult<TransferCardResultDto>> ExecuteAsync(
        int sourceBoardId,
        int sourceCardId,
        TransferCardRequest request,
        int actorUserId)
    {
        if (request.DestinationBoardId == sourceBoardId)
        {
            return ApiErrors.ValidationFailed([
                new ValidationError("destinationBoardId", "Destination board must be different from the source board.")
            ]);
        }

        using var scope = scopeFactory.CreateWithTransaction(IsolationLevel.Serializable);

        var canMoveFromSource = await boardAuthorisationService.HasPermissionAsync(
            sourceBoardId,
            actorUserId,
            BoardPermission.CardMove);
        if (!canMoveFromSource)
        {
            return ApiErrors.Forbidden("You do not have permission to move this card.");
        }

        var destinationBoard = boardRepository.Get(request.DestinationBoardId);
        if (destinationBoard is null)
        {
            return ApiErrors.ValidationFailed([
                new ValidationError("destinationBoardId", "Destination board does not exist.")
            ]);
        }

        var canCreateOnDestination = await boardAuthorisationService.HasPermissionAsync(
            request.DestinationBoardId,
            actorUserId,
            BoardPermission.CardCreate);
        if (!canCreateOnDestination)
        {
            return ApiErrors.Forbidden("You do not have permission to create cards on the destination board.");
        }

        var isCopyMissing = string.Equals(
            request.TransferPolicy?.Trim(),
            CardTransferPolicies.CopyMissing,
            StringComparison.OrdinalIgnoreCase);
        if (isCopyMissing)
        {
            var canManageDestination = await boardAuthorisationService.HasPermissionAsync(
                request.DestinationBoardId,
                actorUserId,
                BoardPermission.BoardManageSettings);
            if (!canManageDestination)
            {
                return ApiErrors.Forbidden("Only destination board owners can copy missing definitions.");
            }
        }

        var sourceCard = await cardRepository.GetWithTagsAndBoardAsync(sourceBoardId, sourceCardId);
        if (sourceCard is null)
        {
            return ApiErrors.NotFound("Card not found.");
        }

        var destinationColumn = columnRepository.Get(request.DestinationColumnId);
        if (destinationColumn is null || destinationColumn.BoardId != request.DestinationBoardId)
        {
            return ApiErrors.ValidationFailed([
                new ValidationError("destinationColumnId", "Column does not exist on the destination board.")
            ]);
        }

        var destinationDefaultCardType = await cardTypeRepository.GetSystemByBoardIdAsync(request.DestinationBoardId);
        if (destinationDefaultCardType is null)
        {
            return ApiErrors.InternalError("Destination board default card type was not found.");
        }

        var destinationCardTypes = await cardTypeRepository.GetAllForBoardAsync(request.DestinationBoardId);
        var destinationTags = await tagRepository.GetAllForBoardAsync(request.DestinationBoardId);
        var destinationSlicks = await slickRepository.GetAllForBoardAsync(request.DestinationBoardId);
        var contentPlanResult = contentPlanner.CreatePlan(
            request.TransferPolicy,
            request.DestinationBoardId,
            sourceCard,
            destinationDefaultCardType,
            destinationCardTypes,
            destinationTags,
            destinationSlicks);
        if (contentPlanResult.Error is not null)
        {
            return contentPlanResult.Error;
        }

        var contentPlan = contentPlanResult.Plan!;
        var destinationCards = await cardRepository.GetCardsInColumnOrderedAsync(destinationColumn.Id);
        var orderPlan = insertionOrderPlanner.CreateLeadingPlan(sourceCard, destinationCards);
        if (orderPlan.Error is not null)
        {
            return orderPlan.Error;
        }

        foreach (var cardType in contentPlan.NewCardTypes)
        {
            cardTypeRepository.Add(cardType);
        }

        foreach (var tag in contentPlan.NewTags)
        {
            tagRepository.Add(tag);
        }

        foreach (var slick in contentPlan.NewSlicks)
        {
            slickRepository.Add(slick);
        }

        var destinationMembership = sourceCard.AssignedUserId is int assignedUserId
            ? await boardMemberRepository.GetByBoardAndUserAsync(request.DestinationBoardId, assignedUserId)
            : null;
        var preservedAssigneeId = destinationMembership?.User.IsActive == true
            ? sourceCard.AssignedUserId
            : null;

        var sourceCardNumber = sourceCard.RequireBoardCardId();
        sourceCard.BoardId = request.DestinationBoardId;
        sourceCard.BoardColumnId = destinationColumn.Id;
        sourceCard.BoardColumn = destinationColumn;
        sourceCard.CardType = contentPlan.CardType;
        sourceCard.CardTypeId = contentPlan.CardType.Id;
        sourceCard.Slick = contentPlan.Slick;
        sourceCard.SlickId = contentPlan.Slick?.Id;
        sourceCard.AssignedUserId = preservedAssigneeId;
        if (preservedAssigneeId is null)
        {
            sourceCard.AssignedUser = null;
        }
        sourceCard.CardUpdatedUtc = DateTime.UtcNow;
        CardTagMutation.ReplaceTags(sourceCard, contentPlan.Tags);
        foreach (var assignment in orderPlan.Assignments)
        {
            assignment.Card.SortKey = assignment.SortKey;
        }

        try
        {
            sourceCard.BoardCardId = await boardCardIdAllocator.AllocateNextAsync(request.DestinationBoardId);
            await scope.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return TransferConflict();
        }
        catch (ConcurrencyException)
        {
            return TransferConflict();
        }

        var transferredCard = await CardDtoEnrichment.EnrichAssignedUserImageAsync(
            sourceCard.ToCardDto(),
            imageRepository);
        await boardEvents.CardDeletedAsync(sourceBoardId, sourceCardNumber);
        await boardEvents.CardCreatedAsync(request.DestinationBoardId, transferredCard);
        if (contentPlan.CreatedDefinitions || orderPlan.Renormalised)
        {
            await boardEvents.ResyncRequestedAsync(request.DestinationBoardId);
        }

        return ApiResults.Ok(new TransferCardResultDto(request.DestinationBoardId, transferredCard));
    }

    private static ApiError TransferConflict() =>
        new(
            409,
            "The card or destination changed while the card was being moved. Reload and try again.");
}
