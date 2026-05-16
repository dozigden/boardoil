using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Contracts;
using BoardOil.Data.Abstractions.Card;

namespace BoardOil.Services.Card;

public sealed class BulkDeleteCardsService(
    ICardRepository cardRepository,
    IBoardAuthorisationService boardAuthorisationService,
    IBoardEvents boardEvents,
    IDbContextScopeFactory scopeFactory)
{
    public async Task<ApiResult<BulkDeleteCardsSummaryDto>> ExecuteAsync(int boardId, BulkDeleteCardsRequest request, int actorUserId)
    {
        using var scope = scopeFactory.Create();

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
            return ApiErrors.ValidationFailed([new ValidationError("cardIds", "One or more cards do not exist in board.")]);
        }

        cardRepository.RemoveRange(cards);
        await scope.SaveChangesAsync();

        foreach (var cardId in uniqueCardIds)
        {
            await boardEvents.CardDeletedAsync(boardId, cardId);
        }

        return ApiResults.Ok(new BulkDeleteCardsSummaryDto(boardId, uniqueCardIds.Count, uniqueCardIds.Count));
    }}
