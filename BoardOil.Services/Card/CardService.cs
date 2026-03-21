using BoardOil.Abstractions;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Tag;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Contracts;
using BoardOil.Contracts.Tag;
using BoardOil.Services.Ordering;

namespace BoardOil.Services.Card;

public sealed class CardService(
    ICardRepository repository,
    ICardValidator validator,
    ITagRepository tagRepository,
    IBoardEvents boardEvents,
    IDbContextScopeFactory scopeFactory) : ICardService
{
    private readonly ITagRepository _tagRepository = tagRepository;
    private readonly IBoardEvents _boardEvents = boardEvents;
    private readonly IDbContextScopeFactory _scopeFactory = scopeFactory;

    public async Task<ApiResult<CardDto>> CreateCardAsync(CreateCardRequest request)
    {
        using var scope = _scopeFactory.Create();

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

        var createTagErrors = await ValidateTagNamesExistAsync(request.TagNames);
        if (createTagErrors.Count > 0)
        {
            return ValidationFail(createTagErrors);
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
        repository.Add(new CreateCardRecord(
            BoardColumnId: request.BoardColumnId,
            Title: request.Title.Trim(),
            Description: request.Description,
            SortKey: sortKey!,
            TagNames: request.TagNames ?? Array.Empty<string>(),
            CreatedAtUtc: now,
            UpdatedAtUtc: now));

        await scope.SaveChangesAsync();

        var cardsAfterCreate = (await repository.GetCardsInColumnOrderedAsync(request.BoardColumnId)).ToList();
        var createdRecord = cardsAfterCreate.FirstOrDefault(x => x.SortKey == sortKey);
        if (createdRecord is null)
        {
            return ApiErrors.InternalError("Created card could not be reloaded.");
        }

        var createdIndex = FindCardIndex(cardsAfterCreate, createdRecord.Id);
        var created = createdRecord.ToCardDto(insertIndex);
        if (createdIndex >= 0)
        {
            created = createdRecord.ToCardDto(createdIndex);
        }

        await _boardEvents.CardCreatedAsync(created);
        return ApiResults.Created(created);
    }

    public async Task<ApiResult<CardDto>> UpdateCardAsync(int id, UpdateCardRequest request)
    {
        using var scope = _scopeFactory.Create();

        var existingCard = await repository.GetByIdAsync(id);
        if (existingCard is null)
        {
            return ApiErrors.NotFound("Card not found.");
        }

        var updateValidationErrors = validator.ValidateUpdate(request);
        if (updateValidationErrors.Count > 0)
        {
            return ValidationFail(updateValidationErrors);
        }

        var updatedTitle = request.Title is null ? existingCard.Title : request.Title.Trim();
        var updatedDescription = request.Description ?? existingCard.Description;
        IReadOnlyList<string>? updatedTagNames = null;
        if (request.TagNames is not null)
        {
            var updateTagErrors = await ValidateTagNamesExistAsync(request.TagNames);
            if (updateTagErrors.Count > 0)
            {
                return ValidationFail(updateTagErrors);
            }

            updatedTagNames = request.TagNames;
        }

        var tagsChanged = updatedTagNames is not null
            && !existingCard.TagNames.SequenceEqual(updatedTagNames, StringComparer.Ordinal);
        var metadataChanged = updatedTitle != existingCard.Title
            || updatedDescription != existingCard.Description
            || tagsChanged;

        var sourceColumnId = existingCard.BoardColumnId;
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
        string targetSortKey;
        if (targetColumnId == sourceColumnId)
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
                targetColumnId,
                request.Position);
            operationError = moveResult.Error;
            targetSortKey = moveResult.SortKey;
        }

        if (operationError is not null)
        {
            return operationError;
        }

        var movementRequested = request.BoardColumnId is not null || request.Position is not null;
        var changed = metadataChanged
            || targetColumnId != existingCard.BoardColumnId
            || targetSortKey != existingCard.SortKey;

        var updatedCard = existingCard with
        {
            BoardColumnId = targetColumnId,
            Title = updatedTitle,
            Description = updatedDescription,
            SortKey = targetSortKey,
            TagNames = updatedTagNames ?? existingCard.TagNames,
            UpdatedAtUtc = changed ? DateTime.UtcNow : existingCard.UpdatedAtUtc
        };

        if (changed)
        {
            await repository.UpdateAsync(new UpdateCardRecord(
                Id: updatedCard.Id,
                BoardColumnId: updatedCard.BoardColumnId,
                Title: updatedCard.Title,
                Description: updatedCard.Description,
                SortKey: updatedCard.SortKey,
                TagNames: updatedTagNames,
                UpdatedAtUtc: updatedCard.UpdatedAtUtc));

            await scope.SaveChangesAsync();
        }

        var finalPosition = await GetCardPositionAsync(updatedCard.BoardColumnId, updatedCard.Id);
        var dto = updatedCard.ToCardDto(finalPosition < 0 ? 0 : finalPosition);
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
        using var scope = _scopeFactory.Create();

        var card = await repository.GetByIdAsync(id);
        if (card is null)
        {
            return ApiResults.Ok();
        }

        await repository.DeleteAsync(id);
        await scope.SaveChangesAsync();
        await _boardEvents.CardDeletedAsync(id);

        return ApiResults.Ok();
    }

    private static ApiError ValidationFail(IReadOnlyList<ValidationError> validationErrors) =>
        ApiErrors.BadRequest("Validation failed.", validationErrors);

    private async Task<bool> EnsureTargetColumnExistsAsync(int sourceColumnId, int targetColumnId)
    {
        if (targetColumnId == sourceColumnId)
        {
            return true;
        }

        return await repository.ColumnExistsAsync(targetColumnId);
    }

    private static ApiError? UpdateSortKeyWithinColumn(
        int? requestedPosition,
        List<CardRecord> sourceCards,
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
        var targetCards = (await repository.GetCardsInColumnOrderedAsync(targetColumnId)).ToList();

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

    private static int FindCardIndex(IReadOnlyList<CardRecord> cards, int targetId)
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

    private async Task<IReadOnlyList<ValidationError>> ValidateTagNamesExistAsync(IReadOnlyList<string>? tagNames)
    {
        if (tagNames is null || tagNames.Count == 0)
        {
            return Array.Empty<ValidationError>();
        }

        var missingTagErrors = new List<ValidationError>();
        foreach (var tagName in tagNames)
        {
            var existingTag = await _tagRepository.GetByNormalisedNameAsync(tagName.ToUpperInvariant());
            if (existingTag is null)
            {
                missingTagErrors.Add(new ValidationError("tagNames", $"Tag '{tagName}' does not exist."));
            }
        }

        return missingTagErrors.Count == 0 ? Array.Empty<ValidationError>() : missingTagErrors;
    }
}
