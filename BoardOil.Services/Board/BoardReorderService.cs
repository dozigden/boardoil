using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Data.Abstractions.Card;
using BoardOil.Data.Abstractions.Column;
using BoardOil.Services.Card;
using BoardOil.Services.Ordering;

namespace BoardOil.Services.Board;

public sealed class BoardReorderService(
    IBoardRepository boardRepository,
    IColumnRepository columnRepository,
    ICardRepository cardRepository,
    IBoardAuthorisationService authorisation,
    IBoardEvents boardEvents,
    IDbContextScopeFactory scopeFactory)
{
    public async Task<ApiResult> ExecuteAsync(int boardId, ReorderBoardRequest request, int actorUserId)
    {
        using var scope = scopeFactory.CreateWithTransaction(System.Data.IsolationLevel.Serializable);
        if (!await authorisation.HasPermissionAsync(boardId, actorUserId, BoardPermission.CardMove))
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var errors = ValidateShape(request.Columns);
        if (errors.Count > 0)
        {
            return ApiErrors.ValidationFailed(errors);
        }

        var requested = request.Columns!.Select(column => column!).ToArray();
        var columns = await columnRepository.GetColumnsInBoardOrderedAsync(boardId);
        var columnIds = columns.Select(column => column.Id).ToHashSet();
        if (requested.Length != columns.Count || requested.Any(column => !columnIds.Contains(column.ColumnId!.Value)))
        {
            return ApiErrors.ValidationFailed([new ValidationError("columns", "Include every current column exactly once. Refresh the board and try again.")]);
        }

        var cards = await cardRepository.GetCardsForColumnsOrderedAsync(columnIds.ToArray());
        var cardsById = cards.ToDictionary(card => card.RequireBoardCardId());
        var requestedCardIds = requested.SelectMany(column => column.CardIds!).ToArray();
        if (requestedCardIds.Length != cards.Count || requestedCardIds.Any(id => !cardsById.ContainsKey(id)))
        {
            return ApiErrors.ValidationFailed([new ValidationError("columns", "Include every current live card exactly once. Refresh the board and try again.")]);
        }

        var cardsByColumn = cards.GroupBy(card => card.BoardColumnId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var changedColumns = requested.Where(column =>
            !cardsByColumn.GetValueOrDefault(column.ColumnId!.Value, [])
                .Select(card => card.RequireBoardCardId()).SequenceEqual(column.CardIds!)).ToArray();
        if (changedColumns.Length == 0)
        {
            return ApiResults.Ok();
        }

        var originalPositions = cardsByColumn.Values.SelectMany(column => column.Select((card, index) =>
            (Id: card.RequireBoardCardId(), ColumnId: card.BoardColumnId, Index: index)))
            .ToDictionary(position => position.Id);
        // Exclude all old board keys so cross-column swaps cannot collide mid-save.
        var occupiedKeys = cards.Select(card => card.SortKey).ToHashSet(StringComparer.Ordinal);
        var now = DateTime.UtcNow;
        foreach (var column in changedColumns)
        {
            var keys = SortKeyGenerator.CreateEvenlySpaced(column.CardIds!.Count, occupiedKeys);
            for (var index = 0; index < column.CardIds.Count; index++)
            {
                var card = cardsById[column.CardIds[index]];
                var original = originalPositions[card.RequireBoardCardId()];
                card.BoardColumnId = column.ColumnId!.Value;
                card.SortKey = keys[index];
                if (original.ColumnId != card.BoardColumnId || original.Index != index)
                {
                    card.CardUpdatedUtc = now;
                }
            }
        }

        await scope.SaveChangesAsync();
        await boardEvents.ResyncRequestedAsync(boardId);
        return ApiResults.Ok();
    }

    private static List<ValidationError> ValidateShape(IReadOnlyList<BoardCardOrder?>? columns)
    {
        var errors = new List<ValidationError>();
        if (columns is null)
        {
            errors.Add(new ValidationError("columns", "Columns are required."));
            return errors;
        }

        var columnIds = new HashSet<int>();
        var cardIds = new HashSet<int>();
        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            var prefix = $"columns[{index}]";
            if (column is null)
            {
                errors.Add(new ValidationError(prefix, "Column entry is required."));
                continue;
            }

            if (column.ColumnId is not > 0)
            {
                errors.Add(new ValidationError($"{prefix}.columnId", "Column ID must be greater than zero."));
            }
            else if (!columnIds.Add(column.ColumnId.Value))
            {
                errors.Add(new ValidationError($"{prefix}.columnId", "Include each column only once."));
            }

            if (column.CardIds is null)
            {
                errors.Add(new ValidationError($"{prefix}.cardIds", "Card IDs are required; use an empty array for an empty column."));
                continue;
            }

            for (var cardIndex = 0; cardIndex < column.CardIds.Count; cardIndex++)
            {
                var id = column.CardIds[cardIndex];
                if (id <= 0)
                {
                    errors.Add(new ValidationError($"{prefix}.cardIds[{cardIndex}]", "Card ID must be greater than zero."));
                }
                else if (!cardIds.Add(id))
                {
                    errors.Add(new ValidationError($"{prefix}.cardIds[{cardIndex}]", "Include each card only once across the whole board."));
                }
            }
        }

        return errors;
    }
}
