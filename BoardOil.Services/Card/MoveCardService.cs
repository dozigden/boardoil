using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Contracts;
using BoardOil.Data.Abstractions.Card;
using BoardOil.Data.Abstractions.Column;
using BoardOil.Data.Abstractions.Image;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Services.Card;

public sealed class MoveCardService(
    ICardRepository cardRepository,
    IColumnRepository columnRepository,
    IImageRepository imageRepository,
    IBoardAuthorisationService boardAuthorisationService,
    MoveCardPlanner planner,
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

        var existingCard = await cardRepository.GetWithTagsAndBoardAsync(id);
        if (existingCard is null || existingCard.BoardColumn.BoardId != boardId)
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
        var sourceIndex = planner.FindCardIndex(sourceCards, id);
        if (sourceIndex < 0)
        {
            return ApiErrors.NotFound("Card not found.");
        }

        var currentPositionAfterCardId = sourceIndex > 0 ? sourceCards[sourceIndex - 1].Id : (int?)null;
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
                .Where(x => x.Id != id)
                .ToList();
        }
        else
        {
            targetCards = (await cardRepository.GetCardsInColumnOrderedAsync(targetColumn.Id))
                .Where(x => x.Id != id)
                .ToList();
        }

        var anchorResolution = planner.ResolveAnchor(request.PositionAfterCardId, targetCards);
        if (anchorResolution.Error is not null)
        {
            return anchorResolution.Error;
        }

        var sortKeyResult = planner.AllocateSortKey(anchorResolution.PreviousKey, anchorResolution.NextKey);
        if (sortKeyResult.Error is not null)
        {
            return sortKeyResult.Error;
        }

        var targetSortKey = sortKeyResult.SortKey!;

        var movementChanged = targetColumn.Id != existingCard.BoardColumnId
            || targetSortKey != existingCard.SortKey;
        if (movementChanged)
        {
            existingCard.BoardColumnId = targetColumn.Id;
            existingCard.SortKey = targetSortKey;

            await scope.SaveChangesAsync();
        }

        var dto = await CardDtoEnrichment.EnrichAssignedUserImageAsync(existingCard.ToCardDto(), imageRepository);
        await boardEvents.CardMovedAsync(boardId, dto);

        return dto;
    }}
