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

        var previousKey = cards.Count > 0 ? cards[^1].SortKey : null;
        var nextKey = (string?)null;
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

        var created = card.ToCardDto();

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

        var dto = existingCard.ToCardDto();
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

        if (request.PositionAfterCardId == id)
        {
            return ValidationFail([new ValidationError("positionAfterCardId", "Card cannot be positioned after itself.")]);
        }

        var sourceCards = (await cardRepository.GetCardsInColumnOrderedAsync(sourceColumnId)).ToList();
        var sourceIndex = FindCardIndex(sourceCards, id);
        if (sourceIndex < 0)
        {
            return ApiErrors.NotFound("Card not found.");
        }

        var currentPositionAfterCardId = sourceIndex > 0 ? sourceCards[sourceIndex - 1].Id : (int?)null;
        if (targetColumn.Id == sourceColumnId
            && request.PositionAfterCardId == currentPositionAfterCardId)
        {
            var unchangedDto = existingCard.ToCardDto();
            await _boardEvents.CardMovedAsync(unchangedDto);
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

        var anchorResolution = ResolveAnchor(request.PositionAfterCardId, targetCards);
        if (anchorResolution.Error is not null)
        {
            return anchorResolution.Error;
        }

        if (!TryGenerateSortKey(
                anchorResolution.PreviousKey,
                anchorResolution.NextKey,
                out var targetSortKeyValue,
                out var allocationError))
        {
            return allocationError!;
        }

        var targetSortKey = targetSortKeyValue!;

        var movementChanged = targetColumn.Id != existingCard.BoardColumnId
            || targetSortKey != existingCard.SortKey;
        if (movementChanged)
        {
            existingCard.BoardColumnId = targetColumn.Id;
            existingCard.SortKey = targetSortKey;
            existingCard.UpdatedAtUtc = DateTime.UtcNow;

            await scope.SaveChangesAsync();
        }

        var dto = existingCard.ToCardDto();
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
            return (ValidationFail([new ValidationError("positionAfterCardId", "Card does not exist in target column.")]), null, null);
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
