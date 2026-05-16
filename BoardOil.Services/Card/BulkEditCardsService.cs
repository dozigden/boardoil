using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Contracts;
using BoardOil.Persistence.Abstractions.Card;
using BoardOil.Persistence.Abstractions.Column;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Persistence.Abstractions.Image;
using BoardOil.Persistence.Abstractions.Tag;
using BoardOil.Services.Ordering;

namespace BoardOil.Services.Card;

public sealed class BulkEditCardsService(
    ICardRepository cardRepository,
    IColumnRepository columnRepository,
    ITagRepository tagRepository,
    IImageRepository imageRepository,
    IBoardAuthorisationService boardAuthorisationService,
    IBoardEvents boardEvents,
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

        if (uniqueCardIds.Count == 0 || (!hasMoveOperation && !hasTagEditOperation))
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

        if (hasTagEditOperation)
        {
            var hasUpdatePermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.CardUpdate);
            if (!hasUpdatePermission)
            {
                return ApiErrors.Forbidden("You do not have permission for this action.");
            }
        }

        var selectedCardIdSet = uniqueCardIds.ToHashSet();
        if (hasMoveOperation && request.Move!.PositionAfterCardId is int positionAfterCardId && selectedCardIdSet.Contains(positionAfterCardId))
        {
            return ApiErrors.ValidationFailed([new ValidationError("move.positionAfterCardId", "Anchor card cannot be one of the moved cards.")]);
        }

        var cards = await cardRepository.GetWithTagsAndBoardByIdsAsync(uniqueCardIds);
        if (cards.Count != uniqueCardIds.Count || cards.Any(x => x.BoardColumn.BoardId != boardId))
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
            ? await CardTagMutation.ResolveTagsAsync(boardId, addTagNames, _tagRepository)
            : [];
        var removeTagNameSet = hasTagEditOperation
            ? removeTagNames.Select(NormaliseTagName).ToHashSet(StringComparer.Ordinal)
            : [];

        string? previousKey = null;
        string? nextKey = null;
        if (hasMoveOperation)
        {
            var targetCards = (await cardRepository.GetCardsInColumnOrderedAsync(targetColumn!.Id))
                .Where(x => !selectedCardIdSet.Contains(x.Id))
                .ToList();
            var anchorResolution = ResolveAnchor(request.Move!.PositionAfterCardId, targetCards);
            if (anchorResolution.Error is not null)
            {
                return anchorResolution.Error;
            }

            previousKey = anchorResolution.PreviousKey;
            nextKey = anchorResolution.NextKey;
        }

        var resultDtos = new List<CardDto>(orderedCards.Count);
        var movedDtos = new List<CardDto>(orderedCards.Count);
        var updatedDtos = new List<CardDto>(orderedCards.Count);

        foreach (var card in orderedCards)
        {
            var movementChanged = false;
            string? targetSortKey = card.SortKey;
            if (hasMoveOperation)
            {
                var sortKeyResult = AllocateSortKey(previousKey, nextKey);
                if (sortKeyResult.Error is not null)
                {
                    return sortKeyResult.Error;
                }

                targetSortKey = sortKeyResult.SortKey!;
                movementChanged = card.BoardColumnId != targetColumn!.Id
                    || card.SortKey != targetSortKey;
                if (movementChanged)
                {
                    card.BoardColumnId = targetColumn!.Id;
                    card.SortKey = targetSortKey;
                }

                previousKey = targetSortKey;
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

            var dto = await CardDtoEnrichment.EnrichAssignedUserImageAsync(card.ToCardDto(), imageRepository);
            resultDtos.Add(dto);
            if (movementChanged)
            {
                movedDtos.Add(dto);
            }
            else if (tagsChanged)
            {
                updatedDtos.Add(dto);
            }
        }

        if (movedDtos.Count > 0 || updatedDtos.Count > 0)
        {
            await scope.SaveChangesAsync();
            foreach (var dto in movedDtos)
            {
                await boardEvents.CardMovedAsync(boardId, dto);
            }

            foreach (var dto in updatedDtos)
            {
                await boardEvents.CardUpdatedAsync(boardId, dto);
            }
        }

        return resultDtos;
    }
    private static (ApiError? Error, string? PreviousKey, string? NextKey) ResolveAnchor(
        int? positionAfterCardId,
        IReadOnlyList<EntityBoardCard> targetCards)
    {
        if (positionAfterCardId is null)
        {
            var firstSortKey = targetCards.Count > 0 ? targetCards[0].SortKey : null;
            return (null, null, firstSortKey);
        }

        var anchorIndex = FindCardIndex(targetCards, positionAfterCardId.Value);
        if (anchorIndex < 0)
        {
            return (ApiErrors.ValidationFailed([new ValidationError("positionAfterCardId", "Card does not exist in target column.")]), null, null);
        }

        var previousKey = targetCards[anchorIndex].SortKey;
        var nextKey = anchorIndex < targetCards.Count - 1
            ? targetCards[anchorIndex + 1].SortKey
            : null;
        return (null, previousKey, nextKey);
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

    private static (string? SortKey, ApiError? Error) AllocateSortKey(string? previous, string? next)
    {
        try
        {
            return (SortKeyGenerator.Between(previous, next), null);
        }
        catch (InvalidOperationException)
        {
            return (null, ApiErrors.InternalError("Unable to assign card order key."));
        }
        catch (ArgumentException)
        {
            return (null, ApiErrors.InternalError("Unable to assign card order key."));
        }
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
