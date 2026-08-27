using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Data.Abstractions.Card;
using BoardOil.Data.Abstractions.CardType;
using BoardOil.Data.Abstractions.Column;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Image;
using BoardOil.Data.Abstractions.Slick;
using BoardOil.Data.Abstractions.Tag;
using BoardOil.Data.Abstractions.Users;
using BoardOil.Services.Style;
using BoardOil.Services.Users;
using System.Text;
using System.Text.Json;

namespace BoardOil.Services.Card;

public sealed class CardArchiveService(
    ICardRepository cardRepository,
    ICardCommentRepository cardCommentRepository,
    IArchivedCardRepository archivedCardRepository,
    ICardTypeRepository cardTypeRepository,
    IColumnRepository columnRepository,
    IBoardMemberRepository boardMemberRepository,
    IUserRepository userRepository,
    IImageRepository imageRepository,
    ISlickRepository slickRepository,
    ITagRepository tagRepository,
    IBoardAuthorisationService boardAuthorisationService,
    IBoardEvents boardEvents,
    CardInsertionOrderPlanner insertionOrderPlanner,
    IDbContextScopeFactory scopeFactory) : ICardArchiveService
{
    private const int MaxArchiveSnapshotJsonBytes = 2_097_152;
    private const int MaxCardTitleLength = 200;
    private const int MaxCardDescriptionLength = 20_000;
    private const int MaxSlickNameLength = 40;
    private const int MaxCommentLength = 4_000;
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

    public async Task<ApiResult<ArchivedCardDetailDto>> GetArchivedCardAsync(int boardId, int boardCardId, int actorUserId)
    {
        using var scope = scopeFactory.CreateReadOnly();

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardAccess);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have access to this board.");
        }

        var archivedCard = await archivedCardRepository.GetByBoardCardIdAsync(boardId, boardCardId);
        if (archivedCard is null)
        {
            return ApiErrors.NotFound("Archived card not found.");
        }

        var parsed = ArchivedCardSnapshotSerialiser.TryBuildCurrentSnapshot(archivedCard.SnapshotJson, out var snapshot, out var snapshotReadError);
        if (!parsed || snapshot is null)
        {
            return ApiErrors.InternalError(snapshotReadError ?? "Archived card snapshot is invalid.");
        }

        var currentSnapshotCard = await ResolveCurrentSnapshotCardAsync(
            boardId,
            snapshot.Card,
            snapshot.OriginalColumnName,
            snapshot.AssignedUserEmail);
        currentSnapshotCard = currentSnapshotCard with { Id = archivedCard.OriginalCardId };
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
            return ApiErrors.ValidationFailed(validationErrors);
        }

        var archiveResult = await ExecuteArchiveCardsAsync(boardId, cardIds!, actorUserId);
        if (archiveResult.Error is not null)
        {
            return archiveResult.Error;
        }

        return ApiResults.Ok(new ArchiveCardsSummaryDto(boardId, cardIds!.Count, archiveResult.ArchivedCards!.Count));
    }

    public async Task<ApiResult<CardDto>> UnarchiveCardAsync(int boardId, int boardCardId, int actorUserId)
    {
        using var scope = scopeFactory.Create();

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.CardCreate);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var archivedCard = await archivedCardRepository.GetByBoardCardIdForUpdateAsync(boardId, boardCardId);
        if (archivedCard is null)
        {
            return ApiErrors.NotFound("Archived card not found.");
        }

        var parsed = ArchivedCardSnapshotSerialiser.TryBuildCurrentSnapshot(archivedCard.SnapshotJson, out var snapshot, out var snapshotReadError);
        if (!parsed || snapshot is null)
        {
            return ApiErrors.BadRequest($"Archived card snapshot cannot be restored. {snapshotReadError ?? "Snapshot is invalid."}");
        }
        var snapshotCard = snapshot.Card;

        var targetColumn = await ResolveRestoreColumnAsync(boardId, snapshot.OriginalColumnName);
        if (targetColumn is null)
        {
            return ApiErrors.BadRequest("Board does not contain any columns.");
        }

        var selectedCardType = await ResolveCardTypeAsync(boardId, snapshotCard.CardTypeName);
        if (selectedCardType is null)
        {
            return ApiErrors.InternalError("System card type not found for board.");
        }

        var validationErrors = ValidateSnapshotCardData(snapshotCard);
        validationErrors.AddRange(ValidateSnapshotComments(snapshot.Comments));
        if (validationErrors.Count > 0)
        {
            return ApiErrors.BadRequest("Archived card snapshot cannot be restored.", validationErrors);
        }

        var title = snapshotCard.Title.Trim();
        var cardsInColumn = await cardRepository.GetCardsInColumnOrderedAsync(targetColumn.Id);
        var resolvedAssignedUser = await ResolveAssignedUserAsync(boardId, snapshot.AssignedUserEmail);
        var resolvedSlick = await ResolveSlickByNameAsync(boardId, snapshotCard.SlickName);
        var resolvedTags = await ResolveTagsForRestoreAsync(boardId, snapshotCard.TagNames);
        var restoredCard = new EntityBoardCard
        {
            BoardId = boardId,
            BoardCardId = archivedCard.OriginalCardId,
            BoardColumnId = targetColumn.Id,
            BoardColumn = targetColumn,
            CardTypeId = selectedCardType.Id,
            CardType = selectedCardType,
            AssignedUserId = resolvedAssignedUser?.Id,
            AssignedUser = resolvedAssignedUser,
            SlickId = resolvedSlick?.Id,
            Slick = resolvedSlick,
            Title = title,
            Description = snapshotCard.Description,
            ExternalUrl = CardExternalUrl.Normalise(snapshotCard.ExternalUrl),
            SortKey = string.Empty,
            CardCreatedUtc = snapshotCard.CardCreatedUtc,
            CardUpdatedUtc = snapshotCard.CardUpdatedUtc,
        };
        ReplaceTags(restoredCard, resolvedTags);

        var orderPlan = insertionOrderPlanner.CreateLeadingPlan(restoredCard, cardsInColumn);
        if (orderPlan.Error is not null)
        {
            return orderPlan.Error;
        }

        foreach (var assignment in orderPlan.Assignments)
        {
            assignment.Card.SortKey = assignment.SortKey;
        }

        var commentAuthorByNormalisedEmail = new Dictionary<string, EntityUser?>(StringComparer.Ordinal);
        foreach (var snapshotComment in snapshot.Comments)
        {
            var author = await ResolveCommentAuthorForRestoreAsync(snapshotComment, commentAuthorByNormalisedEmail);
            var restoredComment = new EntityCardComment
            {
                Card = restoredCard,
                AuthorUserId = author?.Id,
                AuthorUser = author,
                Text = snapshotComment.Text,
                PostedAtUtc = snapshotComment.CreatedAtUtc,
                CreatedAtUtc = snapshotComment.CreatedAtUtc
            };
            cardCommentRepository.Add(restoredComment);
        }

        cardRepository.Add(restoredCard);
        archivedCardRepository.Remove(archivedCard);
        await scope.SaveChangesAsync();

        var dto = await EnrichAssignedUserImageAsync(restoredCard.ToCardDto());
        await boardEvents.CardCreatedAsync(boardId, dto);
        if (orderPlan.Renormalised)
        {
            await boardEvents.ResyncRequestedAsync(boardId);
        }

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

        var cards = await cardRepository.GetWithTagsAndBoardByIdsAsync(boardId, requestedCardIds);
        if (cards.Count != requestedCardIds.Count)
        {
            return new ArchiveExecutionResult(ApiErrors.NotFound("Card not found."), null);
        }

        var cardsById = cards.ToDictionary(x => x.RequireBoardCardId());
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
            return new ArchivedCardBuildResult(
                null,
                ApiErrors.BadRequest("This card is too large to archive."));
        }

        var searchTitle = card.Title.Trim();
        var searchTagsJson = JsonSerializer.Serialize<IReadOnlyList<string>>(tagNames);
        var searchTextNormalised = BuildNormalisedSearchText(searchTitle, tagNames);
        var archivedCard = new EntityArchivedCard
        {
            BoardId = boardId,
            OriginalCardId = card.RequireBoardCardId(),
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

    private async Task<CardDto> ResolveCurrentSnapshotCardAsync(
        int boardId,
        CardDto snapshotCard,
        string? originalColumnName,
        string? assignedUserEmail)
    {
        var resolvedColumn = await ResolveRestoreColumnAsync(boardId, originalColumnName);
        var resolvedCardType = await ResolveCardTypeAsync(boardId, snapshotCard.CardTypeName);
        var resolvedTags = await ResolveSnapshotTagReferencesAsync(boardId, snapshotCard.TagNames);
        var resolvedSlick = await ResolveSlickByNameAsync(boardId, snapshotCard.SlickName);
        var assignedUser = await ResolveAssignedUserAsync(boardId, assignedUserEmail);
        var resolvedCard = snapshotCard with
        {
            BoardColumnId = resolvedColumn?.Id ?? 0,
            CardTypeId = resolvedCardType?.Id ?? 0,
            CardTypeName = resolvedCardType?.Name ?? snapshotCard.CardTypeName,
            CardTypeEmoji = resolvedCardType?.Emoji,
            Tags = resolvedTags,
            AssignedUserId = null,
            AssignedUserDisplayName = null,
            AssignedUserImageRelativePath = null,
            SlickId = resolvedSlick?.Id,
            SlickName = resolvedSlick?.Name
        };
        if (assignedUser is null)
        {
            return resolvedCard;
        }

        return resolvedCard with
        {
            AssignedUserId = assignedUser.Id,
            AssignedUserDisplayName = assignedUser.DisplayName,
            AssignedUserImageRelativePath = (await imageRepository.GetLatestForEntityAsync(ImageEntityType.UserProfile, assignedUser.Id))?.RelativePath
        };
    }

    private async Task<EntityBoardColumn?> ResolveRestoreColumnAsync(int boardId, string? originalColumnName)
    {
        var columns = await columnRepository.GetColumnsInBoardOrderedAsync(boardId);
        if (!string.IsNullOrWhiteSpace(originalColumnName))
        {
            var canonicalColumnName = originalColumnName.Trim();
            var matchingColumn = columns.FirstOrDefault(
                x => string.Equals(x.Title, canonicalColumnName, StringComparison.OrdinalIgnoreCase));
            if (matchingColumn is not null)
            {
                return matchingColumn;
            }
        }

        return columns.FirstOrDefault();
    }

    private async Task<EntityCardType?> ResolveCardTypeAsync(int boardId, string? cardTypeName)
    {
        if (!string.IsNullOrWhiteSpace(cardTypeName))
        {
            var normalisedName = cardTypeName.Trim().ToUpperInvariant();
            var matchingCardType = await cardTypeRepository.GetByNormalisedNameAsync(boardId, normalisedName);
            if (matchingCardType is not null)
            {
                return matchingCardType;
            }
        }

        return await cardTypeRepository.GetSystemByBoardIdAsync(boardId);
    }

    private async Task<EntityUser?> ResolveAssignedUserAsync(int boardId, string? assignedUserEmail)
    {
        var normalisedEmail = EmailAddressRules.TryNormalise(assignedUserEmail);
        if (normalisedEmail is null)
        {
            return null;
        }

        var user = await userRepository.GetByNormalisedEmailAsync(normalisedEmail);
        if (user is null || !user.IsActive)
        {
            return null;
        }

        var membership = await boardMemberRepository.GetByBoardAndUserAsync(boardId, user.Id);
        return membership?.User is { IsActive: true }
            ? membership.User
            : null;
    }

    private async Task<EntitySlick?> ResolveSlickByNameAsync(int boardId, string? snapshotSlickName)
    {
        if (string.IsNullOrWhiteSpace(snapshotSlickName))
        {
            return null;
        }

        var normalisedName = snapshotSlickName.Trim().ToUpperInvariant();
        return await slickRepository.GetByNormalisedNameAsync(boardId, normalisedName);
    }

    private async Task<IReadOnlyList<CardTagDto>> ResolveSnapshotTagReferencesAsync(
        int boardId,
        IReadOnlyList<string> tagNames)
    {
        var resolvedTags = new List<CardTagDto>();
        var processedNormalisedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tagName in NormaliseTagNames(tagNames))
        {
            var normalisedName = tagName.ToUpperInvariant();
            if (!processedNormalisedNames.Add(normalisedName))
            {
                continue;
            }

            var tag = await tagRepository.GetByNormalisedNameAsync(boardId, normalisedName);
            if (tag is not null)
            {
                resolvedTags.Add(new CardTagDto(tag.Id, tag.Name, tag.StyleName, tag.StylePropertiesJson, tag.Emoji));
            }
        }

        return resolvedTags
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToList();
    }

    private async Task<CardDto> EnrichAssignedUserImageAsync(CardDto card)
    {
        if (card.AssignedUserId is null)
        {
            return card.WithAssignedUserImageRelativePath(null);
        }

        var image = await imageRepository.GetLatestForEntityAsync(ImageEntityType.UserProfile, card.AssignedUserId.Value);
        return card.WithAssignedUserImageRelativePath(image?.RelativePath);
    }

    private async Task<IReadOnlyList<EntityTag>> ResolveTagsForRestoreAsync(int boardId, IReadOnlyList<string> tagNames)
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
                StyleName = StyleDefinitionCodec.PresetsStyleName,
                StylePropertiesJson = StyleDefinitionCodec.Serialise(
                    StyleDefinitionCodec.CreateDefault(StyleKind.Presets)),
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

    private async Task<EntityUser?> ResolveCommentAuthorForRestoreAsync(
        ArchivedCardSnapshotCommentV1Payload snapshotComment,
        IDictionary<string, EntityUser?> authorByNormalisedEmail)
    {
        if (string.IsNullOrWhiteSpace(snapshotComment.AuthorEmail))
        {
            return null;
        }

        var normalisedEmail = EmailAddressRules.TryNormalise(snapshotComment.AuthorEmail);
        if (string.IsNullOrWhiteSpace(normalisedEmail))
        {
            return null;
        }

        if (authorByNormalisedEmail.TryGetValue(normalisedEmail, out var cachedByEmail))
        {
            return cachedByEmail;
        }

        var emailUser = await userRepository.GetByNormalisedEmailAsync(normalisedEmail);
        authorByNormalisedEmail[normalisedEmail] = emailUser;
        return emailUser;
    }

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

        var slickName = snapshotCard.SlickName?.Trim();
        if (!string.IsNullOrEmpty(slickName) && slickName.Length > MaxSlickNameLength)
        {
            errors.Add(new ValidationError("snapshot.slickName", $"Slick name must be {MaxSlickNameLength} characters or fewer."));
        }

        var externalUrlValidationError = CardExternalUrl.ValidateOptional(snapshotCard.ExternalUrl, "snapshot.externalUrl");
        if (externalUrlValidationError is not null)
        {
            errors.Add(externalUrlValidationError);
        }

        return errors;
    }

    private static IReadOnlyList<ValidationError> ValidateSnapshotComments(IReadOnlyList<ArchivedCardSnapshotCommentV1Payload> comments)
    {
        var errors = new List<ValidationError>();
        for (var commentIndex = 0; commentIndex < comments.Count; commentIndex++)
        {
            var comment = comments[commentIndex];
            var propertyPrefix = $"snapshot.comments[{commentIndex}]";
            var text = comment.Text?.Trim() ?? string.Empty;
            if (text.Length == 0)
            {
                errors.Add(new ValidationError($"{propertyPrefix}.text", "Comment text is required."));
                continue;
            }

            if (text.Length > MaxCommentLength)
            {
                errors.Add(new ValidationError(
                    $"{propertyPrefix}.text",
                    $"Comment text must be {MaxCommentLength} characters or fewer."));
            }

            if (comment.CreatedAtUtc == default)
            {
                errors.Add(new ValidationError($"{propertyPrefix}.createdAtUtc", "Comment created time is required."));
            }
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
