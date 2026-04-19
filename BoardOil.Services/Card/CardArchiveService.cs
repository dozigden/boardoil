using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Contracts;
using BoardOil.Persistence.Abstractions.Card;
using BoardOil.Persistence.Abstractions.Entities;
using System.Text;
using System.Text.Json;

namespace BoardOil.Services.Card;

public sealed class CardArchiveService(
    ICardRepository cardRepository,
    IArchivedCardRepository archivedCardRepository,
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

    public async Task<ApiResult<ArchivedCardDto>> GetArchivedCardAsync(int boardId, int archivedCardId, int actorUserId)
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

        return ApiResults.Ok(archivedCard.ToArchivedCardDto());
    }

    public async Task<ApiResult<ArchivedCardDto>> ArchiveCardAsync(int boardId, int id, int actorUserId)
    {
        using var scope = scopeFactory.Create();

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.CardDelete);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var card = await cardRepository.GetWithTagsAndBoardAsync(id);
        if (card is null || card.BoardColumn.BoardId != boardId)
        {
            return ApiErrors.NotFound("Card not found.");
        }

        var archivedAtUtc = DateTime.UtcNow;
        var tagNames = card.CardTags
            .Select(x => x.Tag.Name)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        var snapshotJson = ArchivedCardSnapshotSerialiser.CreateSnapshotJson(boardId, card, archivedAtUtc);
        if (Encoding.UTF8.GetByteCount(snapshotJson) > MaxArchiveSnapshotJsonBytes)
        {
            return ApiErrors.InternalError("Archive snapshot exceeds configured size limit.");
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

        archivedCardRepository.Add(archivedCard);
        cardRepository.Remove(card);
        await scope.SaveChangesAsync();
        await boardEvents.CardDeletedAsync(boardId, id);

        return ApiResults.Ok(archivedCard.ToArchivedCardDto());
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

    private static string NormaliseSearchValue(string value) =>
        value.Trim().ToUpperInvariant();

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
}
