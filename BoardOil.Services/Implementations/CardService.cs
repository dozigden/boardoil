using BoardOil.Ef;
using BoardOil.Ef.Entities;
using BoardOil.Services.Abstractions;
using BoardOil.Services.Contracts;
using BoardOil.Services.Mappings;
using BoardOil.Services.Ordering;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Services.Implementations;

public sealed class CardService(BoardOilDbContext dbContext, ICardValidator validator) : ICardService
{
    public async Task<ApiResult<CardDto>> CreateCardAsync(CreateCardRequest request, CancellationToken cancellationToken = default)
    {
        var validationErrors = validator.ValidateCreate(request);
        if (validationErrors.Count > 0)
        {
            return ValidationFail(validationErrors);
        }

        var column = await dbContext.Columns
            .FirstOrDefaultAsync(x => x.Id == request.BoardColumnId, cancellationToken);

        if (column is null)
        {
            return ApiErrors.NotFound("Column not found.");
        }

        var cards = await dbContext.Cards
            .Where(x => x.BoardColumnId == column.Id)
            .OrderBy(x => x.SortKey)
            .ToListAsync(cancellationToken);

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
            BoardColumnId = column.Id,
            Title = request.Title.Trim(),
            Description = request.Description,
            SortKey = sortKey!,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.Cards.Add(card);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResults.Created(card.ToCardDto(insertIndex));
    }

    public async Task<ApiResult<CardDto>> UpdateCardAsync(int id, UpdateCardRequest request, CancellationToken cancellationToken = default)
    {
        var card = await dbContext.Cards
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

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
            var targetColumnExists = await dbContext.Columns
                .AnyAsync(x => x.Id == targetColumnId, cancellationToken);

            if (!targetColumnExists)
            {
                return ApiErrors.NotFound("Column not found.");
            }
        }

        var sourceCards = await dbContext.Cards
            .Where(x => x.BoardColumnId == sourceColumnId)
            .OrderBy(x => x.SortKey)
            .ToListAsync(cancellationToken);

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
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            else if (titleChanged || descriptionChanged)
            {
                card.UpdatedAtUtc = now;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        else
        {
            var targetCards = await dbContext.Cards
                .Where(x => x.BoardColumnId == targetColumnId)
                .OrderBy(x => x.SortKey)
                .ToListAsync(cancellationToken);

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

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var finalCards = await dbContext.Cards
            .Where(x => x.BoardColumnId == card.BoardColumnId)
            .OrderBy(x => x.SortKey)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var finalPosition = finalCards.FindIndex(cardId => cardId == card.Id);
        return card.ToCardDto(finalPosition < 0 ? 0 : finalPosition);
    }

    public async Task<ApiResult> DeleteCardAsync(int id, CancellationToken cancellationToken = default)
    {
        var card = await dbContext.Cards
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (card is null)
        {
            return ApiErrors.NotFound("Card not found.");
        }

        dbContext.Cards.Remove(card);
        await dbContext.SaveChangesAsync(cancellationToken);

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
