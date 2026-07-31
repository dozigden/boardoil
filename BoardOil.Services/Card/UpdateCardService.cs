using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Data.Abstractions.Card;
using BoardOil.Data.Abstractions.CardType;
using BoardOil.Data.Abstractions.Column;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Image;
using BoardOil.Data.Abstractions.Tag;
using BoardOil.Data.Abstractions.Slick;
using BoardOil.Services.Style;

namespace BoardOil.Services.Card;

public sealed class UpdateCardService(
    IBoardRepository boardRepository,
    ICardRepository cardRepository,
    ICardTypeRepository cardTypeRepository,
    IColumnRepository columnRepository,
    ITagRepository tagRepository,
    ISlickRepository slickRepository,
    IImageRepository imageRepository,
    IBoardAuthorisationService boardAuthorisationService,
    ICardValidator validator,
    UpdateCardPlanner planner,
    CardMoveOrderPlanner orderPlanner,
    SlickCohesionPlacementResolver cohesionPlacementResolver,
    IBoardEvents boardEvents,
    IBoardStyleDefaultService styleDefaultService,
    IDbContextScopeFactory scopeFactory)
{
    private readonly ITagRepository _tagRepository = tagRepository;

    public async Task<ApiResult<CardDto>> ExecuteAsync(int boardId, int id, UpdateCardRequest request, int actorUserId)
    {
        using var scope = scopeFactory.Create();

        var hasUpdatePermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.CardUpdate);
        if (!hasUpdatePermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var existingCard = await cardRepository.GetWithTagsAndBoardAsync(boardId, id);
        if (existingCard is null)
        {
            return ApiErrors.NotFound("Card not found.");
        }

        var updateValidationErrors = await validator.ValidateUpdateAsync(boardId, request);
        if (updateValidationErrors.Count > 0)
        {
            return ApiErrors.ValidationFailed(updateValidationErrors);
        }

        var currentColumnId = existingCard.BoardColumnId;
        var requestedColumnId = request.BoardColumnId ?? currentColumnId;
        if (request.BoardColumnId is int explicitColumnId && explicitColumnId != currentColumnId)
        {
            var hasMovePermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.CardMove);
            if (!hasMovePermission)
            {
                return ApiErrors.Forbidden("You do not have permission for this action.");
            }
        }

        if (request.BoardColumnId is int updateColumnId)
        {
            var targetColumn = columnRepository.Get(updateColumnId);
            if (targetColumn is null || targetColumn.BoardId != boardId)
            {
                return ApiErrors.ValidationFailed([new ValidationError("boardColumnId", "Column does not exist in board.")]);
            }
        }

        var updatedTitle = request.Title.Trim();
        var updatedDescription = request.Description;
        var updatedExternalUrl = CardExternalUrl.Normalise(request.ExternalUrl);
        var updatedTags = await CardTagMutation.ResolveTagsAsync(boardId, request.TagNames, _tagRepository, styleDefaultService);

        var selectedCardType = await cardTypeRepository.GetByIdInBoardAsync(boardId, request.CardTypeId);
        var cardTypeSelection = planner.SelectCardType(selectedCardType);
        if (cardTypeSelection.Error is not null)
        {
            return cardTypeSelection.Error;
        }

        var selectedSlick = await CardSlickMutation.ResolveSlickAsync(boardId, request.SlickName, slickRepository, styleDefaultService);
        var selectedSlickId = selectedSlick?.Id;

        var movementChanged = requestedColumnId != currentColumnId;
        CardMoveOrderPlan? movementPlan = null;
        if (movementChanged)
        {
            var targetCards = (await cardRepository.GetCardsInColumnOrderedAsync(requestedColumnId))
                .Where(x => x.Id != existingCard.Id)
                .ToList();
            var board = boardRepository.Get(boardId);
            var effectivePositionAfterCardId = cohesionPlacementResolver.ResolveEffectivePositionAfterCardId(
                board?.SlickCohesionModeEnabled ?? true,
                isCrossColumnMove: true,
                requestedPositionAfterCardId: null,
                targetCards,
                [existingCard]);
            movementPlan = orderPlanner.CreatePlan(targetCards, [existingCard], effectivePositionAfterCardId);
            if (movementPlan.Error is not null)
            {
                return movementPlan.Error;
            }
        }

        var assignmentChanged = request.AssignedUserId != existingCard.AssignedUserId;
        var slickChanged = selectedSlickId != existingCard.SlickId;
        var tagsChanged = planner.TagsChanged(existingCard, updatedTags);
        var cardTypeChanged = selectedCardType!.Id != existingCard.CardTypeId;
        var metadataChanged = updatedTitle != existingCard.Title
            || updatedDescription != existingCard.Description
            || updatedExternalUrl != existingCard.ExternalUrl
            || tagsChanged
            || cardTypeChanged
            || assignmentChanged
            || slickChanged;
        if (metadataChanged || movementChanged)
        {
            existingCard.Title = updatedTitle;
            existingCard.Description = updatedDescription;
            existingCard.ExternalUrl = updatedExternalUrl;
            if (tagsChanged)
            {
                CardTagMutation.ReplaceTags(existingCard, updatedTags);
            }

            if (cardTypeChanged)
            {
                existingCard.CardTypeId = selectedCardType.Id;
                existingCard.CardType = selectedCardType;
            }

            if (assignmentChanged)
            {
                existingCard.AssignedUserId = request.AssignedUserId;
                existingCard.AssignedUser = null;
            }

            if (slickChanged)
            {
                existingCard.Slick = selectedSlick;
                existingCard.SlickId = selectedSlickId > 0 ? selectedSlickId : null;
            }

            if (movementChanged)
            {
                foreach (var assignment in movementPlan!.Assignments)
                {
                    assignment.Card.SortKey = assignment.SortKey;
                }

                existingCard.BoardColumnId = requestedColumnId;
            }

            await scope.SaveChangesAsync();
        }

        var dto = await CardDtoEnrichment.EnrichAssignedUserImageAsync(existingCard.ToCardDto(), imageRepository);
        if (movementChanged)
        {
            await boardEvents.CardMovedAsync(boardId, dto);
            if (movementPlan!.Renormalised)
            {
                await boardEvents.ResyncRequestedAsync(boardId);
            }
        }
        else
        {
            await boardEvents.CardUpdatedAsync(boardId, dto);
        }

        return dto;
    }}
