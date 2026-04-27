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
using System.Text;
using System.Text.Json;

namespace BoardOil.Services.Card;

public sealed class CardArchiveService(
    ICardRepository cardRepository,
    IArchivedCardRepository archivedCardRepository,
    ICardTypeRepository cardTypeRepository,
    IColumnRepository columnRepository,
    IBoardMemberRepository boardMemberRepository,
    ITagRepository tagRepository,
    IBoardAuthorisationService boardAuthorisationService,
    IBoardEvents boardEvents,
    IDbContextScopeFactory scopeFactory) : ICardArchiveService
{
    private const int MaxArchiveSnapshotJsonBytes = 524_288;
    private const int MaxCardTitleLength = 200;
    private const int MaxCardDescriptionLength = 20_000;
    private const int DefaultListLimit = 50;
    private const int MaxListLimit = 200;

    public async Task<ApiResult<ArchivedCardListDto>> GetArchivedCardsAsync(int boardId, string? search, int? offset, int? limit, int actorUserId)
    {
        using var scope = scopeFactory.CreateReadOnly();

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardAccess);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have access to this board.");
        }

        var paginationValidationErrors = ValidatePagination(offset, limit);
        if (paginationValidationErrors.Count > 0)
        {
            return ApiErrors.BadRequest("Invalid pagination parameters.", paginationValidationErrors);
        }

        var listOffset = offset ?? 0;
        var listLimit = limit ?? DefaultListLimit;
        var normalisedSearch = NormaliseSearchTerm(search);
        var totalCount = await archivedCardRepository.CountByBoardAsync(boardId, normalisedSearch);
        var archivedCards = await archivedCardRepository.ListByBoardAsync(boardId, normalisedSearch, listOffset, listLimit);
        IReadOnlyList<ArchivedCardListItemDto> items = archivedCards
            .Select(x => x.ToArchivedCardListItemDto())
            .ToList();
        return ApiResults.Ok(new ArchivedCardListDto(items, listOffset, listLimit, totalCount));
    }

    public async Task<ApiResult<ArchivedCardDetailDto>> GetArchivedCardAsync(int boardId, int archivedCardId, int actorUserId)
    {
        using var scope = scopeFactory.CreateReadOnly();

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardAccess);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have access to this board.");
        }

        var archivedCard = await archivedCardRepository.GetByIdAsync(boardId, archivedCardId);
        if (archivedCard is null)
        {
            return ApiErrors.NotFound("Archived card not found.");
        }

        var parsed = ArchivedCardSnapshotSerialiser.TryBuildCurrentCardDto(archivedCard.SnapshotJson, out var snapshotCard, out var snapshotReadError);
        if (!parsed || snapshotCard is null)
        {
            return ApiErrors.InternalError(snapshotReadError ?? "Archived card snapshot is invalid.");
        }

        var currentSnapshotCard = await ResolveCurrentSnapshotCardAsync(boardId, snapshotCard);
        return ApiResults.Ok(archivedCard.ToArchivedCardDetailDto(currentSnapshotCard));
    }

    public async Task<ApiResult<ArchivedCardDto>> ArchiveCardAsync(int boardId, int id, int actorUserId)
    {
        var archiveResult = await ExecuteArchiveCardsAsync(boardId, [id], actorUserId);
        if (archiveResult.Error is not null)
        {
            return archiveResult.Error;
        }

        var archivedCard = archiveResult.ArchivedCards![0];
        return ApiResults.Ok(archivedCard.ToArchivedCardDto());
    }

    public async Task<ApiResult<ArchiveCardsSummaryDto>> ArchiveCardsAsync(int boardId, ArchiveCardsRequest request, int actorUserId)
    {
        var cardIds = request?.CardIds;
        var validationErrors = ValidateArchiveCardIds(cardIds);
        if (validationErrors.Count > 0)
        {
            return ApiErrors.BadRequest("Validation failed.", validationErrors);
        }

        var archiveResult = await ExecuteArchiveCardsAsync(boardId, cardIds!, actorUserId);
        if (archiveResult.Error is not null)
        {
            return archiveResult.Error;
        }

        return ApiResults.Ok(new ArchiveCardsSummaryDto(boardId, cardIds!.Count, archiveResult.ArchivedCards!.Count));
    }

    public async Task<ApiResult<CardDto>> UnarchiveCardAsync(int boardId, int archivedCardId, int actorUserId)
    {
        using var scope = scopeFactory.Create();

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.CardCreate);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var archivedCard = await archivedCardRepository.GetByIdForUpdateAsync(boardId, archivedCardId);
        if (archivedCard is null)
        {
            return ApiErrors.NotFound("Archived card not found.");
        }

        var parsed = ArchivedCardSnapshotSerialiser.TryBuildCurrentCardDto(archivedCard.SnapshotJson, out var snapshotCard, out var snapshotReadError);
        if (!parsed || snapshotCard is null)
        {
            return ApiErrors.BadRequest($"Archived card snapshot cannot be restored. {snapshotReadError ?? "Snapshot is invalid."}");
        }

        var targetColumn = await ResolveRestoreColumnAsync(boardId, snapshotCard.BoardColumnId);
        if (targetColumn is null)
        {
            return ApiErrors.BadRequest("Board does not contain any columns.");
        }

        var selectedCardType = await cardTypeRepository.GetByIdInBoardAsync(boardId, snapshotCard.CardTypeId)
            ?? await cardTypeRepository.GetSystemByBoardIdAsync(boardId);
        if (selectedCardType is null)
        {
            return ApiErrors.InternalError("System card type not found for board.");
        }

        var validationErrors = ValidateSnapshotCardData(snapshotCard);
        if (validationErrors.Count > 0)
        {
            return ApiErrors.BadRequest("Archived card snapshot cannot be restored.", validationErrors);
        }

        var title = snapshotCard.Title.Trim();
        var now = DateTime.UtcNow;
        var cardsInColumn = await cardRepository.GetCardsInColumnOrderedAsync(targetColumn.Id);
        var nextSortKey = cardsInColumn.Count > 0 ? cardsInColumn[0].SortKey : null;
        if (!TryGenerateSortKey(null, nextSortKey, out var sortKeyValue, out var sortKeyError))
        {
            return sortKeyError!;
        }

        var resolvedAssignedUser = await ResolveAssignedUserForRestoreAsync(boardId, snapshotCard.AssignedUserId);
        var resolvedTags = await ResolveTagsForRestoreAsync(boardId, snapshotCard.TagNames, now);
        var restoredCard = new EntityBoardCard
        {
            BoardColumnId = targetColumn.Id,
            BoardColumn = targetColumn,
            CardTypeId = selectedCardType.Id,
            CardType = selectedCardType,
            AssignedUserId = resolvedAssignedUser?.Id,
            AssignedUser = resolvedAssignedUser,
            Title = title,
            Description = snapshotCard.Description,
            SortKey = sortKeyValue!,
            CreatedAtUtc = snapshotCard.CreatedAtUtc == default ? now : snapshotCard.CreatedAtUtc,
            UpdatedAtUtc = snapshotCard.UpdatedAtUtc == default ? now : snapshotCard.UpdatedAtUtc
        };
        ReplaceTags(restoredCard, resolvedTags);

        cardRepository.Add(restoredCard);
        archivedCardRepository.Remove(archivedCard);
        await scope.SaveChangesAsync();

        var dto = restoredCard.ToCardDto();
        await boardEvents.CardCreatedAsync(boardId, dto);
        return ApiResults.Ok(dto);
    }

    private async Task<ArchiveExecutionResult> ExecuteArchiveCardsAsync(int boardId, IReadOnlyList<int> requestedCardIds, int actorUserId)
    {
        using var scope = scopeFactory.Create();

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.CardDelete);
        if (!hasPermission)
        {
            return new ArchiveExecutionResult(ApiErrors.Forbidden("You do not have permission for this action."), null);
        }

        var cards = await cardRepository.GetWithTagsAndBoardByIdsAsync(requestedCardIds);
        if (cards.Count != requestedCardIds.Count || cards.Any(x => x.BoardColumn.BoardId != boardId))
        {
            return new ArchiveExecutionResult(ApiErrors.NotFound("Card not found."), null);
        }

        var cardsById = cards.ToDictionary(x => x.Id);
        var orderedCards = requestedCardIds.Select(x => cardsById[x]).ToList();
        var archivedCards = new List<EntityArchivedCard>(orderedCards.Count);
        foreach (var card in orderedCards)
        {
            var archivedAtUtc = DateTime.UtcNow;
            var buildResult = BuildArchivedCardEntity(boardId, card, archivedAtUtc);
            if (buildResult.Error is not null)
            {
                return new ArchiveExecutionResult(buildResult.Error, null);
            }

            archivedCards.Add(buildResult.ArchivedCard!);
        }

        archivedCardRepository.AddRange(archivedCards);
        cardRepository.RemoveRange(orderedCards);
        await scope.SaveChangesAsync();
        foreach (var cardId in requestedCardIds)
        {
            await boardEvents.CardDeletedAsync(boardId, cardId);
        }

        return new ArchiveExecutionResult(null, archivedCards);
    }

    private static ArchivedCardBuildResult BuildArchivedCardEntity(int boardId, EntityBoardCard card, DateTime archivedAtUtc)
    {
        var tagNames = card.CardTags
            .Select(x => x.Tag.Name)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        var snapshotJson = ArchivedCardSnapshotSerialiser.CreateSnapshotJson(boardId, card, archivedAtUtc);
        if (Encoding.UTF8.GetByteCount(snapshotJson) > MaxArchiveSnapshotJsonBytes)
        {
            return new ArchivedCardBuildResult(null, ApiErrors.InternalError("Archive snapshot exceeds configured size limit."));
        }

        var searchTitle = card.Title.Trim();
        var searchTagsJson = JsonSerializer.Serialize<IReadOnlyList<string>>(tagNames);
        var searchTextNormalised = BuildNormalisedSearchText(searchTitle, tagNames);
        var archivedCard = new EntityArchivedCard
        {
            BoardId = boardId,
            OriginalCardId = card.Id,
            ArchivedAtUtc = archivedAtUtc,
            SnapshotJson = snapshotJson,
            SearchTitle = searchTitle,
            SearchTagsJson = searchTagsJson,
            SearchTextNormalised = searchTextNormalised
        };
        return new ArchivedCardBuildResult(archivedCard, null);
    }

    private static string? NormaliseSearchTerm(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        return NormaliseSearchValue(search);
    }

    private static string BuildNormalisedSearchText(string title, IReadOnlyList<string> tagNames)
    {
        var values = new List<string> { NormaliseSearchValue(title) };
        values.AddRange(tagNames.Select(NormaliseSearchValue));
        return string.Join('\n', values.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private async Task<CardDto> ResolveCurrentSnapshotCardAsync(int boardId, CardDto snapshotCard)
    {
        if (snapshotCard.AssignedUserId is null)
        {
            return snapshotCard with { AssignedUserId = null, AssignedUserName = null };
        }

        var membership = await boardMemberRepository.GetByBoardAndUserAsync(boardId, snapshotCard.AssignedUserId.Value);
        if (membership?.User is null)
        {
            return snapshotCard with { AssignedUserId = null, AssignedUserName = null };
        }

        return snapshotCard with
        {
            AssignedUserId = membership.UserId,
            AssignedUserName = membership.User.UserName
        };
    }

    private async Task<EntityBoardColumn?> ResolveRestoreColumnAsync(int boardId, int snapshotBoardColumnId)
    {
        var snapshotColumn = columnRepository.Get(snapshotBoardColumnId);
        if (snapshotColumn is not null && snapshotColumn.BoardId == boardId)
        {
            return snapshotColumn;
        }

        var columns = await columnRepository.GetColumnsInBoardOrderedAsync(boardId);
        return columns.FirstOrDefault();
    }

    private async Task<EntityUser?> ResolveAssignedUserForRestoreAsync(int boardId, int? assignedUserId)
    {
        if (assignedUserId is null)
        {
            return null;
        }

        var membership = await boardMemberRepository.GetByBoardAndUserAsync(boardId, assignedUserId.Value);
        if (membership?.User is null || !membership.User.IsActive)
        {
            return null;
        }

        return membership.User;
    }

    private async Task<IReadOnlyList<EntityTag>> ResolveTagsForRestoreAsync(int boardId, IReadOnlyList<string> tagNames, DateTime now)
    {
        var resolvedTags = new List<EntityTag>();
        var processedNormalisedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tagName in NormaliseTagNames(tagNames))
        {
            var normalisedName = tagName.ToUpperInvariant();
            if (!processedNormalisedNames.Add(normalisedName))
            {
                continue;
            }

            var existingTag = await tagRepository.GetByNormalisedNameAsync(boardId, normalisedName);
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
            tagRepository.Add(createdTag);
            resolvedTags.Add(createdTag);
        }

        return resolvedTags
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static void ReplaceTags(EntityBoardCard card, IReadOnlyList<EntityTag> tags)
    {
        card.CardTags.Clear();
        foreach (var tag in tags.OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            card.CardTags.Add(new EntityCardTag { Tag = tag });
        }
    }

    private static IReadOnlyList<string> NormaliseTagNames(IReadOnlyList<string> tagNames) =>
        tagNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

    private static List<ValidationError> ValidateSnapshotCardData(CardDto snapshotCard)
    {
        var errors = new List<ValidationError>();
        var title = snapshotCard.Title.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            errors.Add(new ValidationError("snapshot.title", "Card title is required."));
        }
        else if (title.Length > MaxCardTitleLength)
        {
            errors.Add(new ValidationError("snapshot.title", $"Card title must be {MaxCardTitleLength} characters or fewer."));
        }
        else if (ContainsControlCharacters(title))
        {
            errors.Add(new ValidationError("snapshot.title", "Card title cannot contain control characters."));
        }

        if (snapshotCard.Description.Length > MaxCardDescriptionLength)
        {
            errors.Add(new ValidationError("snapshot.description", $"Card description must be {MaxCardDescriptionLength} characters or fewer."));
        }

        return errors;
    }

    private static bool ContainsControlCharacters(string value)
    {
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
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

    private static string NormaliseSearchValue(string value) =>
        value.Trim().ToUpperInvariant();

    private static List<ValidationError> ValidateArchiveCardIds(IReadOnlyList<int>? cardIds)
    {
        var errors = new List<ValidationError>();
        if (cardIds is null || cardIds.Count == 0)
        {
            errors.Add(new ValidationError("cardIds", "Card IDs are required."));
            return errors;
        }

        var seenCardIds = new HashSet<int>();
        foreach (var cardId in cardIds)
        {
            if (cardId <= 0)
            {
                errors.Add(new ValidationError("cardIds", "Card IDs must be greater than 0."));
            }

            if (!seenCardIds.Add(cardId))
            {
                errors.Add(new ValidationError("cardIds", $"Card ID '{cardId}' is duplicated."));
            }
        }

        return errors;
    }

    private static List<ValidationError> ValidatePagination(int? offset, int? limit)
    {
        var errors = new List<ValidationError>();

        if (offset is < 0)
        {
            errors.Add(new ValidationError(nameof(offset), "Offset must be 0 or greater."));
        }

        if (limit is < 1)
        {
            errors.Add(new ValidationError(nameof(limit), "Limit must be at least 1 when provided."));
        }

        if (limit is > MaxListLimit)
        {
            errors.Add(new ValidationError(nameof(limit), $"Limit cannot exceed {MaxListLimit}."));
        }

        return errors;
    }

    private sealed record ArchiveExecutionResult(
        ApiError? Error,
        IReadOnlyList<EntityArchivedCard>? ArchivedCards);

    private sealed record ArchivedCardBuildResult(
        EntityArchivedCard? ArchivedCard,
        ApiError? Error);
}
