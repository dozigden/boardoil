using BoardOil.Ef.Entities;
using BoardOil.Services.Abstractions;
using BoardOil.Services.Contracts;
using BoardOil.Services.Mappings;
using BoardOil.Services.Ordering;

namespace BoardOil.Services.Implementations;

public sealed class CardService(ICardRepository repository, ICardValidator validator) : ICardService
{
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

        return ApiResults.Created(card.ToCardDto(insertIndex));
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

        var titleChanged = false;
        if (request.Title is not null)
        {
            var normalizedTitle = request.Title.Trim();
            titleChanged = card.Title != normalizedTitle;
            card.Title = normalizedTitle;
        }

        var descriptionChanged = false;
        if (request.Description is not null)
        {
            descriptionChanged = card.Description != request.Description;
            card.Description = request.Description;
        }

        var sourceColumnId = card.BoardColumnId;
        var targetColumnId = request.BoardColumnId ?? sourceColumnId;
        if (targetColumnId != sourceColumnId)
        {
            var targetColumnExists = await repository.ColumnExistsAsync(targetColumnId);

            if (!targetColumnExists)
            {
                return ApiErrors.NotFound("Column not found.");
            }
        }

        var sourceCards = (await repository.GetCardsInColumnOrderedAsync(sourceColumnId)).ToList();

        var sourceIndex = sourceCards.FindIndex(x => x.Id == id);
        if (sourceIndex < 0)
        {
            return ApiErrors.NotFound("Card not found.");
        }

        var now = DateTime.UtcNow;
        if (targetColumnId == sourceColumnId)
        {
            sourceCards.RemoveAt(sourceIndex);

            var targetIndex = request.Position is null
                ? sourceIndex
                : Math.Clamp(request.Position.Value, 0, sourceCards.Count);

            var previousKey = targetIndex > 0 ? sourceCards[targetIndex - 1].SortKey : null;
            var nextKey = targetIndex < sourceCards.Count ? sourceCards[targetIndex].SortKey : null;
            if (!TryGenerateSortKey(previousKey, nextKey, out var sortKey, out var allocationError))
            {
                return allocationError!;
            }

            if (targetIndex != sourceIndex)
            {
                card.SortKey = sortKey!;
                card.UpdatedAtUtc = now;
                await repository.SaveChangesAsync();
            }
            else if (titleChanged || descriptionChanged)
            {
                card.UpdatedAtUtc = now;
                await repository.SaveChangesAsync();
            }
        }
        else
        {
            var targetCards = (await repository.GetCardsInColumnOrderedAsync(targetColumnId)).ToList();

            sourceCards.RemoveAt(sourceIndex);

            var insertIndex = request.Position is null
                ? targetCards.Count
                : Math.Clamp(request.Position.Value, 0, targetCards.Count);

            var previousKey = insertIndex > 0 ? targetCards[insertIndex - 1].SortKey : null;
            var nextKey = insertIndex < targetCards.Count ? targetCards[insertIndex].SortKey : null;
            if (!TryGenerateSortKey(previousKey, nextKey, out var sortKey, out var allocationError))
            {
                return allocationError!;
            }

            card.BoardColumnId = targetColumnId;
            card.SortKey = sortKey!;
            card.UpdatedAtUtc = now;

            await repository.SaveChangesAsync();
        }

        var finalCards = await repository.GetCardIdsInColumnOrderedAsync(card.BoardColumnId);

        var finalPosition = -1;
        for (var i = 0; i < finalCards.Count; i++)
        {
            if (finalCards[i] == card.Id)
            {
                finalPosition = i;
                break;
            }
        }
        return card.ToCardDto(finalPosition < 0 ? 0 : finalPosition);
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

        return ApiResults.Ok();
    }

    private static ApiError ValidationFail(IReadOnlyList<ValidationError> validationErrors) =>
        ApiErrors.BadRequest(
            "Validation failed.",
            validationErrors
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Property) ? string.Empty : x.Property)
                .ToDictionary(x => x.Key, x => x.Select(y => y.Message).ToArray()));

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
