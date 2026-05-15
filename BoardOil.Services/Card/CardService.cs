using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Contracts;
using BoardOil.Persistence.Abstractions.Board;
using BoardOil.Persistence.Abstractions.Card;
using BoardOil.Persistence.Abstractions.Column;
using BoardOil.Persistence.Abstractions.Image;

namespace BoardOil.Services.Card;

public sealed class CardService(
    ICardRepository cardRepository,
    IImageRepository imageRepository,
    IBoardAuthorisationService boardAuthorisationService,
    CreateCardService createCardService,
    UpdateCardService updateCardService,
    MoveCardService moveCardService,
    BulkEditCardsService bulkEditCardsService,
    IBoardEvents boardEvents,
    IDbContextScopeFactory scopeFactory) : ICardService
{
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
        using var scope = _scopeFactory.Create();

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.CardDelete);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var uniqueCardIds = (request.CardIds ?? [])
            .Distinct()
            .ToList();
        if (uniqueCardIds.Count == 0)
        {
            return ApiResults.Ok(new BulkDeleteCardsSummaryDto(boardId, 0, 0));
        }

        var cards = await cardRepository.GetWithTagsAndBoardByIdsAsync(uniqueCardIds);
        if (cards.Count != uniqueCardIds.Count || cards.Any(x => x.BoardColumn.BoardId != boardId))
        {
            return ValidationFail([new ValidationError("cardIds", "One or more cards do not exist in board.")]);
        }

        cardRepository.RemoveRange(cards);
        await scope.SaveChangesAsync();

        foreach (var cardId in uniqueCardIds)
        {
            await _boardEvents.CardDeletedAsync(boardId, cardId);
        }

        return ApiResults.Ok(new BulkDeleteCardsSummaryDto(boardId, uniqueCardIds.Count, uniqueCardIds.Count));
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

}
