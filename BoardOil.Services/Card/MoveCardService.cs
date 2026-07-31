using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Data.Abstractions.Card;
using BoardOil.Data.Abstractions.Column;
using BoardOil.Data.Abstractions.Image;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Services.Card;

public sealed class MoveCardService(
    IBoardRepository boardRepository,
    ICardRepository cardRepository,
    IColumnRepository columnRepository,
    IImageRepository imageRepository,
    IBoardAuthorisationService boardAuthorisationService,
    CardMoveOrderPlanner orderPlanner,
    SlickCohesionPlacementResolver cohesionPlacementResolver,
    IBoardEvents boardEvents,
    IDbContextScopeFactory scopeFactory)
{
    public async Task<ApiResult<CardDto>> ExecuteAsync(int boardId, int id, MoveCardRequest request, int actorUserId)
    {
        using var scope = scopeFactory.Create();

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.CardMove);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var existingCard = await cardRepository.GetWithTagsAndBoardAsync(boardId, id);
        if (existingCard is null)
        {
            return ApiErrors.NotFound("Card not found.");
        }

        var sourceColumnId = existingCard.BoardColumnId;
        var targetColumn = columnRepository.Get(request.BoardColumnId);
        if (targetColumn is null || targetColumn.BoardId != boardId)
        {
            return ApiErrors.ValidationFailed([new ValidationError("boardColumnId", "Column does not exist in board.")]);
        }

        if (request.PositionAfterCardId == id)
        {
            return ApiErrors.ValidationFailed([new ValidationError("positionAfterCardId", "Card cannot be positioned after itself.")]);
        }

        var sourceCards = (await cardRepository.GetCardsInColumnOrderedAsync(sourceColumnId)).ToList();
        var sourceIndex = orderPlanner.FindCardIndex(sourceCards, id);
        if (sourceIndex < 0)
        {
            return ApiErrors.NotFound("Card not found.");
        }

        var currentPositionAfterCardId = sourceIndex > 0
            ? sourceCards[sourceIndex - 1].RequireBoardCardId()
            : (int?)null;
        if (targetColumn.Id == sourceColumnId
            && request.PositionAfterCardId == currentPositionAfterCardId)
        {
            var unchangedDto = await CardDtoEnrichment.EnrichAssignedUserImageAsync(existingCard.ToCardDto(), imageRepository);
            await boardEvents.CardMovedAsync(boardId, unchangedDto);
            return unchangedDto;
        }

        List<EntityBoardCard> targetCards;
        if (targetColumn.Id == sourceColumnId)
        {
            targetCards = sourceCards
                .Where(x => x.Id != existingCard.Id)
                .ToList();
        }
        else
        {
            targetCards = (await cardRepository.GetCardsInColumnOrderedAsync(targetColumn.Id))
                .Where(x => x.Id != existingCard.Id)
                .ToList();
        }

        var board = boardRepository.Get(boardId);
        var effectivePositionAfterCardId = cohesionPlacementResolver.ResolveEffectivePositionAfterCardId(
            board?.SlickCohesionModeEnabled ?? true,
            targetColumn.Id != sourceColumnId,
            request.PositionAfterCardId,
            targetCards,
            [existingCard]);

        var orderPlan = orderPlanner.CreatePlan(targetCards, [existingCard], effectivePositionAfterCardId);
        if (orderPlan.Error is not null)
        {
            return orderPlan.Error;
        }

        var movingCardAssignment = orderPlan.Assignments.Single(assignment => assignment.Card.Id == existingCard.Id);
        var movementChanged = targetColumn.Id != existingCard.BoardColumnId
            || movingCardAssignment.SortKey != existingCard.SortKey;
        if (movementChanged)
        {
            foreach (var assignment in orderPlan.Assignments)
            {
                assignment.Card.SortKey = assignment.SortKey;
            }

            existingCard.BoardColumnId = targetColumn.Id;

            await scope.SaveChangesAsync();
        }

        var dto = await CardDtoEnrichment.EnrichAssignedUserImageAsync(existingCard.ToCardDto(), imageRepository);
        await boardEvents.CardMovedAsync(boardId, dto);
        if (orderPlan.Renormalised)
        {
            await boardEvents.ResyncRequestedAsync(boardId);
        }

        return dto;
    }}
