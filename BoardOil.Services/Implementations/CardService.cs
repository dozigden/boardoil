using BoardOil.Ef.Entities;
using BoardOil.Services.Abstractions;
using BoardOil.Services.Contracts;
using BoardOil.Services.Mappings;
using BoardOil.Services.Ordering;

namespace BoardOil.Services.Implementations;

public sealed class CardService(
    ICardRepository repository,
    ICardValidator validator,
    IBoardEvents boardEvents) : ICardService
{
    private readonly IBoardEvents _boardEvents = boardEvents;

    public async Task<ApiResult<CardDto>> CreateCardAsync(CreateCardRequest request)
    {
        var validationErrors = validator.ValidateCreate(request);
        if (validationErrors.Count > 0)
        {
            return ValidationFail(validationErrors);
        }

        var columnExists = await repository.ColumnExistsAsync(request.BoardColumnId);
        if (!columnExists)
        {
            return ApiErrors.NotFound("Column not found.");
        }

        var cards = (await repository.GetCardsInColumnOrderedAsync(request.BoardColumnId)).ToList();

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
        var card = new BoardCard
        {
            BoardColumnId = request.BoardColumnId,
            Title = request.Title.Trim(),
            Description = request.Description,
            SortKey = sortKey!,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        repository.Add(card);
        await repository.SaveChangesAsync();

        var created = card.ToCardDto(insertIndex);
        await _boardEvents.CardCreatedAsync(created);
        return ApiResults.Created(created);
    }

    public async Task<ApiResult<CardDto>> UpdateCardAsync(int id, UpdateCardRequest request)
    {
        var card = await repository.GetByIdAsync(id);
        if (card is null)
        {
            return ApiErrors.NotFound("Card not found.");
        }

        var updateValidationErrors = validator.ValidateUpdate(request);
        if (updateValidationErrors.Count > 0)
        {
            return ValidationFail(updateValidationErrors);
        }

        var metadataChanged = ApplyMetadataUpdates(card, request);

        var sourceColumnId = card.BoardColumnId;
        var targetColumnId = request.BoardColumnId ?? sourceColumnId;
        var targetExists = await EnsureTargetColumnExistsAsync(sourceColumnId, targetColumnId);
        if (!targetExists)
        {
            return ApiErrors.NotFound("Column not found.");
        }

        var sourceCards = (await repository.GetCardsInColumnOrderedAsync(sourceColumnId)).ToList();
        var sourceIndex = FindCardIndex(sourceCards, id);
        if (sourceIndex < 0)
        {
            return ApiErrors.NotFound("Card not found.");
        }

        ApiError? operationError;
        if (targetColumnId == sourceColumnId)
        {
            operationError = await UpdateWithinColumnAsync(card, request.Position, sourceCards, sourceIndex, metadataChanged);
        }
        else
        {
            operationError = await MoveAcrossColumnsAsync(card, targetColumnId, request.Position, sourceCards, sourceIndex);
        }

        if (operationError is not null)
        {
            return operationError;
        }

        var finalPosition = await GetCardPositionAsync(card.BoardColumnId, card.Id);
        var dto = card.ToCardDto(finalPosition < 0 ? 0 : finalPosition);

        var movementRequested = request.BoardColumnId is not null || request.Position is not null;
        if (movementRequested)
        {
            await _boardEvents.CardMovedAsync(dto);
        }
        else
        {
            await _boardEvents.CardUpdatedAsync(dto);
        }

        return dto;
    }

    public async Task<ApiResult> DeleteCardAsync(int id)
    {
        var card = await repository.GetByIdAsync(id);

        if (card is null)
        {
            return ApiErrors.NotFound("Card not found.");
        }

        repository.Remove(card);
        await repository.SaveChangesAsync();
        await _boardEvents.CardDeletedAsync(id);

        return ApiResults.Ok();
    }

    private static ApiError ValidationFail(IReadOnlyList<ValidationError> validationErrors) =>
        ApiErrors.BadRequest(
            "Validation failed.",
            validationErrors
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Property) ? string.Empty : x.Property)
                .ToDictionary(x => x.Key, x => x.Select(y => y.Message).ToArray()));

    private static bool ApplyMetadataUpdates(BoardCard card, UpdateCardRequest request)
    {
        var changed = false;
        if (request.Title is not null)
        {
            var normalizedTitle = request.Title.Trim();
            changed = changed || card.Title != normalizedTitle;
            card.Title = normalizedTitle;
        }

        if (request.Description is not null)
        {
            changed = changed || card.Description != request.Description;
            card.Description = request.Description;
        }

        return changed;
    }

    private async Task<bool> EnsureTargetColumnExistsAsync(int sourceColumnId, int targetColumnId)
    {
        if (targetColumnId == sourceColumnId)
        {
            return true;
        }

        return await repository.ColumnExistsAsync(targetColumnId);
    }

    private async Task<ApiError?> UpdateWithinColumnAsync(
        BoardCard card,
        int? requestedPosition,
        List<BoardCard> sourceCards,
        int sourceIndex,
        bool metadataChanged)
    {
        sourceCards.RemoveAt(sourceIndex);
        var targetIndex = requestedPosition is null
            ? sourceIndex
            : Math.Clamp(requestedPosition.Value, 0, sourceCards.Count);

        if (targetIndex == sourceIndex)
        {
            if (!metadataChanged)
            {
                return null;
            }

            card.UpdatedAtUtc = DateTime.UtcNow;
            await repository.SaveChangesAsync();
            return null;
        }

        var previousKey = targetIndex > 0 ? sourceCards[targetIndex - 1].SortKey : null;
        var nextKey = targetIndex < sourceCards.Count ? sourceCards[targetIndex].SortKey : null;
        if (!TryGenerateSortKey(previousKey, nextKey, out var sortKey, out var allocationError))
        {
            return allocationError;
        }

        card.SortKey = sortKey!;
        card.UpdatedAtUtc = DateTime.UtcNow;
        await repository.SaveChangesAsync();
        return null;
    }

    private async Task<ApiError?> MoveAcrossColumnsAsync(
        BoardCard card,
        int targetColumnId,
        int? requestedPosition,
        List<BoardCard> sourceCards,
        int sourceIndex)
    {
        var targetCards = (await repository.GetCardsInColumnOrderedAsync(targetColumnId)).ToList();
        sourceCards.RemoveAt(sourceIndex);

        var insertIndex = requestedPosition is null
            ? targetCards.Count
            : Math.Clamp(requestedPosition.Value, 0, targetCards.Count);

        var previousKey = insertIndex > 0 ? targetCards[insertIndex - 1].SortKey : null;
        var nextKey = insertIndex < targetCards.Count ? targetCards[insertIndex].SortKey : null;
        if (!TryGenerateSortKey(previousKey, nextKey, out var sortKey, out var allocationError))
        {
            return allocationError;
        }

        card.BoardColumnId = targetColumnId;
        card.SortKey = sortKey!;
        card.UpdatedAtUtc = DateTime.UtcNow;
        await repository.SaveChangesAsync();
        return null;
    }

    private async Task<int> GetCardPositionAsync(int columnId, int cardId)
    {
        var finalCards = await repository.GetCardIdsInColumnOrderedAsync(columnId);
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

    private static int FindCardIndex(IReadOnlyList<BoardCard> cards, int targetId)
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
}
