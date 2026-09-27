using System.Data;
using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Services.Card;
using Microsoft.Extensions.Logging;

namespace BoardOil.Services.Board;

public sealed class BoardResaveService(
    IDbContextScopeFactory scopes,
    IBoardRepository boards,
    UpdateCardService cards,
    IBoardEvents events,
    ILogger<BoardResaveService> logger) : IBoardResaveService
{
    public async Task<BoardResaveResult> ResaveAsync(int boardId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(boardId);
        cancellationToken.ThrowIfCancellationRequested();
        var examined = 0;
        var changed = 0;
        using (var scope = scopes.CreateWithTransaction(IsolationLevel.Serializable))
        {
            var board = await boards.GetForResaveAsync(boardId, cancellationToken);
            if (board is null)
            {
                logger.LogInformation("Skipped resaving deleted board {BoardId}.", boardId);
                return new BoardResaveResult(boardId, true, 0, 0);
            }

            foreach (var card in board.Columns.SelectMany(column => column.Cards).ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                examined++;
                var result = await cards.SaveAsync(card, new UpdateCardRequest(
                    card.Title, card.Description, card.CardTags.Select(link => link.Tag.Name).ToArray(),
                    card.CardTypeId, card.BoardColumnId, card.AssignedUserId, card.Slick?.Name, card.ExternalUrl),
                    cancellationToken);
                if (!result.Success)
                {
                    var details = result.ValidationErrors?.SelectMany(field =>
                        field.Value.Select(message => $"{field.Key}: {message}")) ?? [];
                    throw new InvalidOperationException(
                        $"Could not resave card #{card.BoardCardId} on board {boardId}: {result.Message} {string.Join("; ", details)}");
                }
                if (result.Data!.Changed)
                {
                    changed++;
                }
            }

            await scope.SaveChangesAsync(cancellationToken);
        }

        if (changed > 0)
        {
            try { await events.ResyncRequestedAsync(boardId); }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not publish resync for resaved board {BoardId}.", boardId);
            }
        }

        return new BoardResaveResult(boardId, false, examined, changed);
    }
}
