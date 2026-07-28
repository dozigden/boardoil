using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Data.Abstractions.Card;
using BoardOil.Data.Abstractions.Column;
using BoardOil.Data.Abstractions.Image;

namespace BoardOil.Services.Card;

public sealed class CardService(
    ICardRepository cardRepository,
    IImageRepository imageRepository,
    IBoardAuthorisationService boardAuthorisationService,
    CreateCardService createCardService,
    UpdateCardService updateCardService,
    MoveCardService moveCardService,
    BulkEditCardsService bulkEditCardsService,
    BulkDeleteCardsService bulkDeleteCardsService,
    IBoardEvents boardEvents,
    IDbContextScopeFactory scopeFactory) : ICardService
{
    private const int MaxSearchFilterCount = 10;

    private readonly IBoardEvents _boardEvents = boardEvents;
    private readonly IDbContextScopeFactory _scopeFactory = scopeFactory;

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

        return await CardDtoEnrichment.EnrichAssignedUserImageAsync(card.ToCardDto(), imageRepository);
    }

    public async Task<ApiResult<IReadOnlyList<CardDto>>> SearchCardsAsync(
        int boardId,
        SearchCardsRequest request,
        int actorUserId)
    {
        using var scope = _scopeFactory.CreateReadOnly();

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardAccess);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have access to this board.");
        }

        if (request.Filters is null || request.Filters.Count == 0)
        {
            return ApiErrors.ValidationFailed([
                new ValidationError("filters", "At least one search filter is required.")
            ]);
        }
        if (request.Filters.Count > MaxSearchFilterCount)
        {
            return ApiErrors.ValidationFailed([
                new ValidationError("filters", $"No more than {MaxSearchFilterCount} search filters are allowed.")
            ]);
        }

        var criteria = new List<CardSearchCriterion>();
        var validationErrors = new List<ValidationError>();
        for (var index = 0; index < request.Filters.Count; index++)
        {
            var filter = request.Filters[index];
            var fieldPath = $"filters[{index}]";
            if (filter is null)
            {
                validationErrors.Add(new ValidationError(fieldPath, "Search filter is required."));
                continue;
            }

            var field = filter.Field?.Trim() ?? string.Empty;
            if (!field.Equals(CardSearchFields.ExternalUrl, StringComparison.OrdinalIgnoreCase))
            {
                validationErrors.Add(new ValidationError(
                    $"{fieldPath}.field",
                    $"Search field must be '{CardSearchFields.ExternalUrl}'."));
            }

            var searchOperator = filter.Operator?.Trim() ?? string.Empty;
            CardSearchOperator? parsedOperator = null;
            if (searchOperator.Equals(CardSearchOperators.Exact, StringComparison.OrdinalIgnoreCase))
            {
                parsedOperator = CardSearchOperator.Exact;
            }
            else if (searchOperator.Equals(CardSearchOperators.Contains, StringComparison.OrdinalIgnoreCase))
            {
                parsedOperator = CardSearchOperator.Contains;
            }
            else
            {
                validationErrors.Add(new ValidationError(
                    $"{fieldPath}.operator",
                    $"Search operator must be '{CardSearchOperators.Exact}' or '{CardSearchOperators.Contains}'."));
            }

            var value = filter.Value?.Trim() ?? string.Empty;
            if (value.Length == 0)
            {
                validationErrors.Add(new ValidationError(
                    $"{fieldPath}.value",
                    "Search filter value is required."));
            }

            if (field.Equals(CardSearchFields.ExternalUrl, StringComparison.OrdinalIgnoreCase) &&
                parsedOperator is not null &&
                value.Length > 0)
            {
                criteria.Add(new CardSearchCriterion(
                    CardSearchField.ExternalUrl,
                    parsedOperator.Value,
                    value));
            }
        }

        if (validationErrors.Count > 0)
        {
            return ApiErrors.ValidationFailed(validationErrors);
        }

        var cards = await cardRepository.SearchAsync(boardId, criteria);
        IReadOnlyList<CardDto> cardDtos = cards.Select(x => x.ToCardDto()).ToList();
        var enrichedCards = await CardDtoEnrichment.EnrichAssignedUserImagesAsync(cardDtos, imageRepository);
        return ApiResults.Ok(enrichedCards);
    }

    public async Task<ApiResult<CardDto>> CreateCardAsync(int boardId, CreateCardRequest request, int actorUserId)
    {
        return await createCardService.ExecuteAsync(boardId, request, actorUserId);
    }

    public async Task<ApiResult<CardDto>> UpdateCardAsync(int boardId, int id, UpdateCardRequest request, int actorUserId)
    {
        return await updateCardService.ExecuteAsync(boardId, id, request, actorUserId);
    }

    public async Task<ApiResult<CardDto>> MoveCardAsync(int boardId, int id, MoveCardRequest request, int actorUserId)
    {
        return await moveCardService.ExecuteAsync(boardId, id, request, actorUserId);
    }

    public async Task<ApiResult<IReadOnlyList<CardDto>>> BulkEditCardsAsync(int boardId, BulkEditCardsRequest request, int actorUserId)
    {
        return await bulkEditCardsService.ExecuteAsync(boardId, request, actorUserId);
    }

    public async Task<ApiResult<BulkDeleteCardsSummaryDto>> BulkDeleteCardsAsync(int boardId, BulkDeleteCardsRequest request, int actorUserId)
    {
        return await bulkDeleteCardsService.ExecuteAsync(boardId, request, actorUserId);
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

}
