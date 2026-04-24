using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Contracts;
using BoardOil.Persistence.Abstractions.Board;
using BoardOil.Persistence.Abstractions.Card;
using BoardOil.Persistence.Abstractions.CardType;
using BoardOil.Persistence.Abstractions.Column;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Persistence.Abstractions.Tag;
using BoardOil.Services.Ordering;
using BoardOil.Services.Tag;

namespace BoardOil.Services.Card;

public sealed class CardService(
    ICardRepository cardRepository,
    ICardTypeRepository cardTypeRepository,
    IBoardMemberRepository boardMemberRepository,
    IColumnRepository columnRepository,
    ITagRepository tagRepository,
    IBoardAuthorisationService boardAuthorisationService,
    ICardValidator validator,
    IBoardEvents boardEvents,
    IDbContextScopeFactory scopeFactory) : ICardService
{
    private readonly IBoardEvents _boardEvents = boardEvents;
    private readonly IDbContextScopeFactory _scopeFactory = scopeFactory;
    private readonly ICardTypeRepository _cardTypeRepository = cardTypeRepository;
    private readonly IBoardMemberRepository _boardMemberRepository = boardMemberRepository;
    private readonly ITagRepository _tagRepository = tagRepository;

    public async Task<ApiResult<CardDto>> GetCardAsync(int boardId, int id, int actorUserId)
    {
        using var scope = _scopeFactory.CreateReadOnly();

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardAccess);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have access to this board.");
        }

        var card = await cardRepository.GetWithTagsAndBoardAsync(id);
        if (card is null || card.BoardColumn.BoardId != boardId)
        {
            return ApiErrors.NotFound("Card not found.");
        }

        return card.ToCardDto();
    }

    public async Task<ApiResult<CardDto>> CreateCardAsync(int boardId, CreateCardRequest request, int actorUserId)
    {
        using var scope = _scopeFactory.Create();

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.CardCreate);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var validationErrors = await validator.ValidateCreateAsync(request);
        if (validationErrors.Count > 0)
        {
            return ValidationFail(validationErrors);
        }

        var targetColumn = request.BoardColumnId is int boardColumnId
            ? columnRepository.Get(boardColumnId)
            : (await columnRepository.GetColumnsInBoardOrderedAsync(boardId)).FirstOrDefault();
        if (targetColumn is null)
        {
            var message = request.BoardColumnId is null
                ? "Board does not contain any columns."
                : "Column does not exist in board.";
            return ValidationFail([new ValidationError("boardColumnId", message)]);
        }

        if (targetColumn.BoardId != boardId)
        {
            return ValidationFail([new ValidationError("boardColumnId", "Column does not exist in board.")]);
        }

        var requestedCardType = request.CardTypeId is null
            ? null
            : await _cardTypeRepository.GetByIdInBoardAsync(boardId, request.CardTypeId.Value);
        if (request.CardTypeId is not null && requestedCardType is null)
        {
            return ValidationFail([new ValidationError("cardTypeId", "Card type does not exist in board.")]);
        }

        var systemCardType = await _cardTypeRepository.GetSystemByBoardIdAsync(boardId);
        if (systemCardType is null)
        {
            return ApiErrors.InternalError("System card type not found for board.");
        }

        var selectedCardType = requestedCardType ?? systemCardType;
        var assignmentResolution = await ResolveAssignedUserAsync(boardId, request.AssignedUserId);
        if (assignmentResolution.Error is not null)
        {
            return assignmentResolution.Error;
        }

        var cards = (await cardRepository.GetCardsInColumnOrderedAsync(targetColumn.Id)).ToList();

        var previousKey = (string?)null;
        var nextKey = cards.Count > 0 ? cards[0].SortKey : null;
        if (!TryGenerateSortKey(previousKey, nextKey, out var sortKey, out var allocationError))
        {
            return allocationError!;
        }

        var now = DateTime.UtcNow;
        var description = request.Description ?? string.Empty;
        var tags = await ResolveTagsAsync(boardId, request.TagNames ?? Array.Empty<string>(), now);
        var card = new EntityBoardCard
        {
            BoardColumnId = targetColumn.Id,
            CardTypeId = selectedCardType.Id,
            CardType = selectedCardType,
            AssignedUserId = request.AssignedUserId,
            AssignedUser = assignmentResolution.AssignedUser,
            Title = request.Title.Trim(),
            Description = description,
            SortKey = sortKey!,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        ReplaceTags(card, tags);

        cardRepository.Add(card);

        await scope.SaveChangesAsync();

        var created = card.ToCardDto();

        await _boardEvents.CardCreatedAsync(boardId, created);
        return ApiResults.Created(created);
    }

    public async Task<ApiResult<CardDto>> UpdateCardAsync(int boardId, int id, UpdateCardRequest request, int actorUserId)
    {
        using var scope = _scopeFactory.Create();

        var hasUpdatePermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.CardUpdate);
        if (!hasUpdatePermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var existingCard = await cardRepository.GetWithTagsAndBoardAsync(id);
        if (existingCard is null || existingCard.BoardColumn.BoardId != boardId)
        {
            return ApiErrors.NotFound("Card not found.");
        }

        var updateValidationErrors = await validator.ValidateUpdateAsync(request);
        if (updateValidationErrors.Count > 0)
        {
            return ValidationFail(updateValidationErrors);
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
                return ValidationFail([new ValidationError("boardColumnId", "Column does not exist in board.")]);
            }
        }

        var updatedTitle = request.Title.Trim();
        var updatedDescription = request.Description;
        var now = DateTime.UtcNow;
        var updatedTags = await ResolveTagsAsync(boardId, request.TagNames, now);
        var selectedCardType = await _cardTypeRepository.GetByIdInBoardAsync(boardId, request.CardTypeId);
        if (selectedCardType is null)
        {
            return ValidationFail([new ValidationError("cardTypeId", "Card type does not exist in board.")]);
        }

        EntityUser? resolvedAssignedUser = existingCard.AssignedUser;
        var assignmentChanged = request.AssignedUserId != existingCard.AssignedUserId;
        if (assignmentChanged)
        {
            var assignmentResolution = await ResolveAssignedUserAsync(boardId, request.AssignedUserId);
            if (assignmentResolution.Error is not null)
            {
                return assignmentResolution.Error;
            }

            resolvedAssignedUser = assignmentResolution.AssignedUser;
        }

        var movementChanged = requestedColumnId != currentColumnId;
        string? targetSortKey = null;
        if (movementChanged)
        {
            var targetCards = (await cardRepository.GetCardsInColumnOrderedAsync(requestedColumnId))
                .Where(x => x.Id != id)
                .ToList();
            var nextKey = targetCards.Count > 0 ? targetCards[0].SortKey : null;
            if (!TryGenerateSortKey(null, nextKey, out targetSortKey, out var allocationError))
            {
                return allocationError!;
            }
        }

        var updatedTagNames = updatedTags
            .Select(x => x.Name)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        var existingTagNames = GetOrderedTagNames(existingCard);
        var tagsChanged = !existingTagNames.SequenceEqual(updatedTagNames, StringComparer.Ordinal);
        var cardTypeChanged = selectedCardType.Id != existingCard.CardTypeId;
        var metadataChanged = updatedTitle != existingCard.Title
            || updatedDescription != existingCard.Description
            || tagsChanged
            || cardTypeChanged
            || assignmentChanged;
        if (metadataChanged || movementChanged)
        {
            existingCard.Title = updatedTitle;
            existingCard.Description = updatedDescription;
            if (tagsChanged)
            {
                ReplaceTags(existingCard, updatedTags);
            }

            if (cardTypeChanged)
            {
                existingCard.CardTypeId = selectedCardType.Id;
                existingCard.CardType = selectedCardType;
            }

            if (assignmentChanged)
            {
                existingCard.AssignedUserId = request.AssignedUserId;
                existingCard.AssignedUser = resolvedAssignedUser;
            }

            if (movementChanged)
            {
                existingCard.BoardColumnId = requestedColumnId;
                existingCard.SortKey = targetSortKey!;
            }

            existingCard.UpdatedAtUtc = now;
            await scope.SaveChangesAsync();
        }

        var dto = existingCard.ToCardDto();
        if (movementChanged)
        {
            await _boardEvents.CardMovedAsync(boardId, dto);
        }
        else
        {
            await _boardEvents.CardUpdatedAsync(boardId, dto);
        }

        return dto;
    }

    public async Task<ApiResult<CardDto>> MoveCardAsync(int boardId, int id, MoveCardRequest request, int actorUserId)
    {
        using var scope = _scopeFactory.Create();

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
            return ValidationFail([new ValidationError("boardColumnId", "Column does not exist in board.")]);
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
            await _boardEvents.CardMovedAsync(boardId, unchangedDto);
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
        await _boardEvents.CardMovedAsync(boardId, dto);

        return dto;
    }

    public async Task<ApiResult> DeleteCardAsync(int boardId, int id, int actorUserId)
    {
        using var scope = _scopeFactory.Create();

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.CardDelete);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var card = await cardRepository.GetWithTagsAndBoardAsync(id);
        if (card is null)
        {
            return ApiResults.Ok();
        }

        if (card.BoardColumn.BoardId != boardId)
        {
            return ApiErrors.NotFound("Card not found.");
        }

        cardRepository.Remove(card);
        await scope.SaveChangesAsync();
        await _boardEvents.CardDeletedAsync(boardId, id);

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
            .Select(x => x.Tag.Name)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

    private static void ReplaceTags(EntityBoardCard card, IReadOnlyList<EntityTag> tags)
    {
        card.CardTags.Clear();
        foreach (var tag in tags.OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            card.CardTags.Add(new EntityCardTag { Tag = tag });
        }
    }

    private async Task<IReadOnlyList<EntityTag>> ResolveTagsAsync(int boardId, IReadOnlyList<string> tagNames, DateTime now)
    {
        var resolvedTags = new List<EntityTag>();
        var processedNormalisedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tagName in NormalizeTags(tagNames))
        {
            var normalisedName = NormaliseTagName(tagName);
            if (!processedNormalisedNames.Add(normalisedName))
            {
                continue;
            }

            var existingTag = await _tagRepository.GetByNormalisedNameAsync(boardId, normalisedName);
            if (existingTag is not null)
            {
                resolvedTags.Add(existingTag);
                continue;
            }

            var createdTag = new EntityTag
            {
                BoardId = boardId,
                Name = tagName,
                NormalisedName = normalisedName,
                StyleName = TagStyleSchemaValidator.SolidStyleName,
                StylePropertiesJson = TagStyleSchemaValidator.BuildDefaultStylePropertiesJson(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            _tagRepository.Add(createdTag);
            resolvedTags.Add(createdTag);
        }

        return resolvedTags
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToList();
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

    private async Task<(ApiError? Error, EntityUser? AssignedUser)> ResolveAssignedUserAsync(int boardId, int? assignedUserId)
    {
        if (assignedUserId is null)
        {
            return (null, null);
        }

        var membership = await _boardMemberRepository.GetByBoardAndUserAsync(boardId, assignedUserId.Value);
        if (membership is null || !membership.User.IsActive)
        {
            return (ValidationFail([new ValidationError("assignedUserId", "Assigned user must be an active board member.")]), null);
        }

        return (null, membership.User);
    }

}
