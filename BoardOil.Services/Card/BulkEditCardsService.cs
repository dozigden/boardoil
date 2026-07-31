using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Data.Abstractions.Card;
using BoardOil.Data.Abstractions.Column;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Image;
using BoardOil.Data.Abstractions.Slick;
using BoardOil.Data.Abstractions.Tag;
using BoardOil.Services.Slick;
using BoardOil.Services.Style;

namespace BoardOil.Services.Card;

public sealed class BulkEditCardsService(
    IBoardRepository boardRepository,
    ICardRepository cardRepository,
    IColumnRepository columnRepository,
    ITagRepository tagRepository,
    ISlickRepository slickRepository,
    IImageRepository imageRepository,
    IBoardAuthorisationService boardAuthorisationService,
    CardMoveOrderPlanner orderPlanner,
    SlickCohesionPlacementResolver cohesionPlacementResolver,
    IBoardEvents boardEvents,
    IBoardStyleDefaultService styleDefaultService,
    IDbContextScopeFactory scopeFactory)
{
    private readonly ITagRepository _tagRepository = tagRepository;

    public async Task<ApiResult<IReadOnlyList<CardDto>>> ExecuteAsync(int boardId, BulkEditCardsRequest request, int actorUserId)
    {
        using var scope = scopeFactory.Create();

        var uniqueCardIds = (request.CardIds ?? [])
            .Distinct()
            .ToList();
        var hasMoveOperation = request.Move is not null;
        var addTagNames = NormalizeTags(request.AddTagNames ?? []);
        var removeTagNames = NormalizeTags(request.RemoveTagNames ?? []);
        var hasTagEditOperation = addTagNames.Count > 0 || removeTagNames.Count > 0;
        var hasSlickEditOperation = request.Slick is not null;

        if (uniqueCardIds.Count == 0 || (!hasMoveOperation && !hasTagEditOperation && !hasSlickEditOperation))
        {
            return ApiResults.Ok<IReadOnlyList<CardDto>>([]);
        }

        if (hasMoveOperation)
        {
            var hasMovePermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.CardMove);
            if (!hasMovePermission)
            {
                return ApiErrors.Forbidden("You do not have permission for this action.");
            }
        }

        if (hasTagEditOperation || hasSlickEditOperation)
        {
            var hasUpdatePermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.CardUpdate);
            if (!hasUpdatePermission)
            {
                return ApiErrors.Forbidden("You do not have permission for this action.");
            }
        }

        if (hasSlickEditOperation)
        {
            var slickNameValidationError = SlickNameValidation.ValidateOptional(request.Slick!.Name, "slick.name");
            if (slickNameValidationError is not null)
            {
                return ApiErrors.ValidationFailed([slickNameValidationError]);
            }
        }

        var selectedCardIdSet = uniqueCardIds.ToHashSet();
        if (hasMoveOperation && request.Move!.PositionAfterCardId is int positionAfterCardId && selectedCardIdSet.Contains(positionAfterCardId))
        {
            return ApiErrors.ValidationFailed([new ValidationError("move.positionAfterCardId", "Anchor card cannot be one of the moved cards.")]);
        }

        var cards = await cardRepository.GetWithTagsAndBoardByIdsAsync(boardId, uniqueCardIds);
        if (cards.Count != uniqueCardIds.Count)
        {
            return ApiErrors.ValidationFailed([new ValidationError("cardIds", "One or more cards do not exist in board.")]);
        }

        EntityBoardColumn? targetColumn = null;
        if (hasMoveOperation)
        {
            targetColumn = columnRepository.Get(request.Move!.TargetColumnId);
            if (targetColumn is null || targetColumn.BoardId != boardId)
            {
                return ApiErrors.ValidationFailed([new ValidationError("move.targetColumnId", "Column does not exist in board.")]);
            }
        }

        var columns = await columnRepository.GetColumnsInBoardOrderedAsync(boardId);
        var columnOrder = columns
            .Select((column, index) => (column.Id, index))
            .ToDictionary(x => x.Id, x => x.index);
        var orderedCards = cards
            .OrderBy(x => columnOrder.GetValueOrDefault(x.BoardColumnId, int.MaxValue))
            .ThenBy(x => x.SortKey, StringComparer.Ordinal)
            .ToList();

        var addTagEntities = hasTagEditOperation
            ? await CardTagMutation.ResolveTagsAsync(boardId, addTagNames, _tagRepository, styleDefaultService)
            : [];
        var removeTagNameSet = hasTagEditOperation
            ? removeTagNames.Select(NormaliseTagName).ToHashSet(StringComparer.Ordinal)
            : [];
        var selectedSlick = hasSlickEditOperation
            ? await CardSlickMutation.ResolveSlickAsync(boardId, request.Slick!.Name, slickRepository, styleDefaultService)
            : null;
        var selectedSlickId = selectedSlick?.Id;

        CardMoveOrderPlan? orderPlan = null;
        IReadOnlyDictionary<int, string> movingSortKeysByCardId = new Dictionary<int, string>();
        if (hasMoveOperation)
        {
            var targetCards = (await cardRepository.GetCardsInColumnOrderedAsync(targetColumn!.Id))
                .Where(x => !selectedCardIdSet.Contains(x.RequireBoardCardId()))
                .ToList();
            var board = boardRepository.Get(boardId);
            var isCrossColumnMove = orderedCards.Any(x => x.BoardColumnId != targetColumn!.Id);
            var effectivePositionAfterCardId = cohesionPlacementResolver.ResolveEffectivePositionAfterCardId(
                board?.SlickCohesionModeEnabled ?? true,
                isCrossColumnMove,
                request.Move!.PositionAfterCardId,
                targetCards,
                orderedCards);
            orderPlan = orderPlanner.CreatePlan(targetCards, orderedCards, effectivePositionAfterCardId);
            if (orderPlan.Error is not null)
            {
                return orderPlan.Error;
            }

            movingSortKeysByCardId = orderPlan.Assignments
                .Where(assignment => selectedCardIdSet.Contains(assignment.Card.RequireBoardCardId()))
                .ToDictionary(assignment => assignment.Card.RequireBoardCardId(), assignment => assignment.SortKey);
            foreach (var assignment in orderPlan.Assignments)
            {
                if (!selectedCardIdSet.Contains(assignment.Card.RequireBoardCardId()))
                {
                    assignment.Card.SortKey = assignment.SortKey;
                }
            }
        }

        var movedCardIdSet = new HashSet<int>();
        var updatedCardIdSet = new HashSet<int>();

        foreach (var card in orderedCards)
        {
            var boardCardId = card.RequireBoardCardId();
            var movementChanged = false;
            if (hasMoveOperation)
            {
                var targetSortKey = movingSortKeysByCardId[boardCardId];
                movementChanged = card.BoardColumnId != targetColumn!.Id
                    || card.SortKey != targetSortKey;
                if (movementChanged)
                {
                    card.BoardColumnId = targetColumn!.Id;
                    card.SortKey = targetSortKey;
                }
            }

            var tagsChanged = false;
            if (hasTagEditOperation)
            {
                var existingTagMap = card.CardTags
                    .Select(x => x.Tag)
                    .ToDictionary(tag => NormaliseTagName(tag.Name), tag => tag, StringComparer.Ordinal);
                var initialTagNames = existingTagMap.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();

                foreach (var removeTagName in removeTagNameSet)
                {
                    existingTagMap.Remove(removeTagName);
                }

                foreach (var addTag in addTagEntities)
                {
                    var normalisedName = NormaliseTagName(addTag.Name);
                    existingTagMap[normalisedName] = addTag;
                }

                var finalTags = existingTagMap.Values
                    .OrderBy(x => x.Name, StringComparer.Ordinal)
                    .ToList();
                var finalTagNames = finalTags
                    .Select(x => NormaliseTagName(x.Name))
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray();

                tagsChanged = !initialTagNames.SequenceEqual(finalTagNames, StringComparer.Ordinal);
                if (tagsChanged)
                {
                    CardTagMutation.ReplaceTags(card, finalTags);
                }
            }

            var slickChanged = false;
            if (hasSlickEditOperation)
            {
                slickChanged = selectedSlickId != card.SlickId;
                if (slickChanged)
                {
                    card.Slick = selectedSlick;
                    card.SlickId = selectedSlickId > 0 ? selectedSlickId : null;
                }
            }

            if (movementChanged)
            {
                movedCardIdSet.Add(boardCardId);
            }
            else if (tagsChanged || slickChanged)
            {
                updatedCardIdSet.Add(boardCardId);
            }
        }

        if (movedCardIdSet.Count > 0 || updatedCardIdSet.Count > 0 || orderPlan?.Renormalised == true)
        {
            await scope.SaveChangesAsync();
        }

        var resultDtos = new List<CardDto>(orderedCards.Count);
        var movedDtos = new List<CardDto>(movedCardIdSet.Count);
        var updatedDtos = new List<CardDto>(updatedCardIdSet.Count);
        foreach (var card in orderedCards)
        {
            var dto = await CardDtoEnrichment.EnrichAssignedUserImageAsync(card.ToCardDto(), imageRepository);
            resultDtos.Add(dto);
            if (movedCardIdSet.Contains(card.RequireBoardCardId()))
            {
                movedDtos.Add(dto);
            }
            else if (updatedCardIdSet.Contains(card.RequireBoardCardId()))
            {
                updatedDtos.Add(dto);
            }
        }

        foreach (var dto in movedDtos)
        {
            await boardEvents.CardMovedAsync(boardId, dto);
        }

        foreach (var dto in updatedDtos)
        {
            await boardEvents.CardUpdatedAsync(boardId, dto);
        }

        if (orderPlan?.Renormalised == true)
        {
            await boardEvents.ResyncRequestedAsync(boardId);
        }

        return resultDtos;
    }

    private static string NormaliseTagName(string tagName) =>
        tagName.ToUpperInvariant();

    private static IReadOnlyList<string> NormalizeTags(IReadOnlyList<string> tagNames)
    {
        return tagNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
    }
}
