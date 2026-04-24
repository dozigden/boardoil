using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Contracts;
using BoardOil.Persistence.Abstractions.Board;
using BoardOil.Persistence.Abstractions.Card;
using BoardOil.Persistence.Abstractions.Entities;
using System.Text;
using System.Text.Json;

namespace BoardOil.Services.Card;

public sealed class CardArchiveService(
    ICardRepository cardRepository,
    IArchivedCardRepository archivedCardRepository,
    IBoardMemberRepository boardMemberRepository,
    IBoardAuthorisationService boardAuthorisationService,
    IBoardEvents boardEvents,
    IDbContextScopeFactory scopeFactory) : ICardArchiveService
{
    private const int MaxArchiveSnapshotJsonBytes = 524_288;
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
