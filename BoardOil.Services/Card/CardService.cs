using BoardOil.Abstractions;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Contracts;
using BoardOil.Contracts.Tag;
using BoardOil.Persistence.Abstractions.Card;
using BoardOil.Persistence.Abstractions.Column;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Services.Ordering;

namespace BoardOil.Services.Card;

public sealed class CardService(
    ICardRepository cardRepository,
    IColumnRepository columnRepository,
    ICardValidator validator,
    IBoardEvents boardEvents,
    IDbContextScopeFactory scopeFactory) : ICardService
{
    private readonly IBoardEvents _boardEvents = boardEvents;
    private readonly IDbContextScopeFactory _scopeFactory = scopeFactory;

    public async Task<ApiResult<CardDto>> CreateCardAsync(CreateCardRequest request)
    {
        using var scope = _scopeFactory.Create();

        var validationErrors = await validator.ValidateCreateAsync(request);
        if (validationErrors.Count > 0)
        {
            return ValidationFail(validationErrors);
        }

        var cards = (await cardRepository.GetCardsInColumnOrderedAsync(request.BoardColumnId)).ToList();

        var insertIndex = request.Position is null
            ? cards.Count
            : Math.Clamp(request.Position.Value, 0, cards.Count);

        var previousKey = insertIndex > 0 ? cards[insertIndex - 1].SortKey : null;
        var nextKey = insertIndex < cards.Count ? cards[insertIndex].SortKey : null;
        if (!TryGenerateSortKey(previousKey, nextKey, out var sortKey, out var allocationError))
        {
            return allocationError!;
        }

        var now = DateTime.UtcNow;
        var card = new EntityBoardCard
        {
            BoardColumnId = request.BoardColumnId,
            Title = request.Title.Trim(),
            Description = request.Description,
            SortKey = sortKey!,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        ReplaceTagNames(card, request.TagNames ?? Array.Empty<string>());

        cardRepository.Add(card);

        await scope.SaveChangesAsync();

        var createdIndex = await GetCardPositionAsync(card.BoardColumnId, card.Id);
        var created = card.ToCardDto(createdIndex >= 0 ? createdIndex : insertIndex);

        await _boardEvents.CardCreatedAsync(created);
        return ApiResults.Created(created);
    }

    public async Task<ApiResult<CardDto>> UpdateCardAsync(int id, UpdateCardRequest request)
    {
        using var scope = _scopeFactory.Create();

        var existingCard = await cardRepository.GetByIdAsync(id);
        if (existingCard is null)
        {
            return ApiErrors.NotFound("Card not found.");
        }

        var updateValidationErrors = await validator.ValidateUpdateAsync(request);
        if (updateValidationErrors.Count > 0)
        {
            return ValidationFail(updateValidationErrors);
        }

        var existingTagNames = GetOrderedTagNames(existingCard);
        var updatedTitle = request.Title is null ? existingCard.Title : request.Title.Trim();
        var updatedDescription = request.Description ?? existingCard.Description;
        IReadOnlyList<string>? requestedTagNames = request.TagNames;
        IReadOnlyList<string>? normalisedTagNames = request.TagNames is null
            ? null
            : NormalizeTags(request.TagNames);

        var tagsChanged = requestedTagNames is not null
            && !existingTagNames.SequenceEqual(requestedTagNames, StringComparer.Ordinal);
        var metadataChanged = updatedTitle != existingCard.Title
            || updatedDescription != existingCard.Description
            || tagsChanged;

        if (metadataChanged)
        {
            existingCard.Title = updatedTitle;
            existingCard.Description = updatedDescription;
            if (normalisedTagNames is not null)
            {
                ReplaceTagNames(existingCard, normalisedTagNames);
            }

            existingCard.UpdatedAtUtc = DateTime.UtcNow;

            await scope.SaveChangesAsync();
        }

        var finalPosition = await GetCardPositionAsync(existingCard.BoardColumnId, existingCard.Id);
        var dto = existingCard.ToCardDto(finalPosition < 0 ? 0 : finalPosition);
        if (requestedTagNames is not null)
        {
            dto = dto with { TagNames = requestedTagNames };
        }

        await _boardEvents.CardUpdatedAsync(dto);

        return dto;
    }

    public async Task<ApiResult<CardDto>> MoveCardAsync(int id, MoveCardRequest request)
    {
        using var scope = _scopeFactory.Create();

        var existingCard = await cardRepository.GetByIdAsync(id);
        if (existingCard is null)
        {
            return ApiErrors.NotFound("Card not found.");
        }

        var sourceColumnId = existingCard.BoardColumnId;
        var targetColumn = await columnRepository.GetByIdAsync(request.BoardColumnId);
        if (targetColumn is null)
        {
            return ValidationFail([new ValidationError("boardColumnId", "Column does not exist.")]);
        }

        var sourceCards = (await cardRepository.GetCardsInColumnOrderedAsync(sourceColumnId)).ToList();
        var sourceIndex = FindCardIndex(sourceCards, id);
        if (sourceIndex < 0)
        {
            return ApiErrors.NotFound("Card not found.");
        }

        ApiError? operationError;
        string targetSortKey;
        if (targetColumn.Id == sourceColumnId)
        {
            operationError = UpdateSortKeyWithinColumn(
                request.Position,
                sourceCards,
                sourceIndex,
                existingCard.SortKey,
                out targetSortKey);
        }
        else
        {
            var moveResult = await CalculateSortKeyAcrossColumnsAsync(
                targetColumn.Id,
                request.Position);
            operationError = moveResult.Error;
            targetSortKey = moveResult.SortKey;
        }

        if (operationError is not null)
        {
            return operationError;
        }

        var movementChanged = targetColumn.Id != existingCard.BoardColumnId
            || targetSortKey != existingCard.SortKey;
        if (movementChanged)
        {
            existingCard.BoardColumnId = targetColumn.Id;
            existingCard.SortKey = targetSortKey;
            existingCard.UpdatedAtUtc = DateTime.UtcNow;

            await scope.SaveChangesAsync();
        }

        var finalPosition = await GetCardPositionAsync(existingCard.BoardColumnId, existingCard.Id);
        var dto = existingCard.ToCardDto(finalPosition < 0 ? 0 : finalPosition);
        await _boardEvents.CardMovedAsync(dto);

        return dto;
    }

    public async Task<ApiResult> DeleteCardAsync(int id)
    {
        using var scope = _scopeFactory.Create();

        var card = await cardRepository.GetByIdAsync(id);
        if (card is null)
        {
            return ApiResults.Ok();
        }

        cardRepository.Remove(card);
        await scope.SaveChangesAsync();
        await _boardEvents.CardDeletedAsync(id);

        return ApiResults.Ok();
    }

    private static ApiError ValidationFail(IReadOnlyList<ValidationError> validationErrors) =>
        ApiErrors.BadRequest("Validation failed.", validationErrors);

    private static ApiError? UpdateSortKeyWithinColumn(
        int? requestedPosition,
        List<EntityBoardCard> sourceCards,
        int sourceIndex,
        string currentSortKey,
        out string targetSortKey)
    {
        sourceCards.RemoveAt(sourceIndex);
        var targetIndex = requestedPosition is null
            ? sourceIndex
            : Math.Clamp(requestedPosition.Value, 0, sourceCards.Count);

        if (targetIndex == sourceIndex)
        {
            targetSortKey = currentSortKey;
            return null;
        }

        var previousKey = targetIndex > 0 ? sourceCards[targetIndex - 1].SortKey : null;
        var nextKey = targetIndex < sourceCards.Count ? sourceCards[targetIndex].SortKey : null;
        if (!TryGenerateSortKey(previousKey, nextKey, out var sortKey, out var allocationError))
        {
            targetSortKey = currentSortKey;
            return allocationError;
        }

        targetSortKey = sortKey!;
        return null;
    }

    private async Task<(ApiError? Error, string SortKey)> CalculateSortKeyAcrossColumnsAsync(
        int targetColumnId,
        int? requestedPosition)
    {
        var targetCards = (await cardRepository.GetCardsInColumnOrderedAsync(targetColumnId)).ToList();

        var insertIndex = requestedPosition is null
            ? targetCards.Count
            : Math.Clamp(requestedPosition.Value, 0, targetCards.Count);

        var previousKey = insertIndex > 0 ? targetCards[insertIndex - 1].SortKey : null;
        var nextKey = insertIndex < targetCards.Count ? targetCards[insertIndex].SortKey : null;
        if (!TryGenerateSortKey(previousKey, nextKey, out var sortKey, out var allocationError))
        {
            return (allocationError, string.Empty);
        }

        return (null, sortKey!);
    }

    private async Task<int> GetCardPositionAsync(int columnId, int cardId)
    {
        var finalCards = await cardRepository.GetCardIdsInColumnOrderedAsync(columnId);
        return FindCardIndex(finalCards, cardId);
    }

    private static int FindCardIndex(IReadOnlyList<int> cardIds, int targetId)
    {
        for (var i = 0; i < cardIds.Count; i++)
        {
            if (cardIds[i] == targetId)
            {
                return i;
            }
        }

        return -1;
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

    private static bool TryGenerateSortKey(string? previous, string? next, out string? sortKey, out ApiError? error)
    {
        try
        {
            sortKey = SortKeyGenerator.Between(previous, next);
            error = null;
            return true;
        }
        catch (InvalidOperationException)
        {
            sortKey = null;
            error = ApiErrors.InternalError("Unable to assign card order key.");
            return false;
        }
        catch (ArgumentException)
        {
            sortKey = null;
            error = ApiErrors.InternalError("Unable to assign card order key.");
            return false;
        }
    }

    private static IReadOnlyList<string> GetOrderedTagNames(EntityBoardCard card) =>
        card.CardTags
            .Select(x => x.TagName)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

    private static IReadOnlyList<string> NormalizeTags(IReadOnlyList<string> tagNames) =>
        tagNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

    private static void ReplaceTagNames(EntityBoardCard card, IReadOnlyList<string> tagNames)
    {
        card.CardTags.Clear();
        foreach (var tagName in NormalizeTags(tagNames))
        {
            card.CardTags.Add(new EntityCardTag { TagName = tagName });
        }
    }
}
