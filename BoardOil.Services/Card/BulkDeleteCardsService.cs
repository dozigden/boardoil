using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Card;

namespace BoardOil.Services.Card;

public sealed class BulkDeleteCardsService(
    ICardRepository cardRepository,
    IBoardAuthorisationService boardAuthorisationService,
    IBoardEvents boardEvents,
    IDbContextScopeFactory scopeFactory,
    BoardOil.Services.Attachment.CardAttachmentService attachments)
{
    public async Task<ApiResult<BulkDeleteCardsSummaryDto>> ExecuteAsync(int boardId, BulkDeleteCardsRequest request, int actorUserId)
    {
        using var scope = scopeFactory.CreateWithTransaction(System.Data.IsolationLevel.Serializable);

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

        var cards = await cardRepository.GetWithTagsAndBoardByIdsAsync(boardId, uniqueCardIds);
        if (cards.Count != uniqueCardIds.Count)
        {
            return ApiErrors.ValidationFailed([new ValidationError("cardIds", "One or more cards do not exist in board.")]);
        }

        var deletedAttachmentKeys = await attachments.DeleteForCardsAsync(cards.Select(x => x.Id).ToList());
        cardRepository.RemoveRange(cards);
        await scope.SaveChangesAsync();
        await attachments.DeleteFilesAsync(deletedAttachmentKeys);

        foreach (var cardId in uniqueCardIds)
        {
            await boardEvents.CardDeletedAsync(boardId, cardId);
        }

        return ApiResults.Ok(new BulkDeleteCardsSummaryDto(boardId, uniqueCardIds.Count, uniqueCardIds.Count));
    }
}
