using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Contracts;
using BoardOil.Persistence.Abstractions.Card;
using BoardOil.Persistence.Abstractions.Board;
using BoardOil.Persistence.Abstractions.Column;
using BoardOil.Services.Card;

namespace BoardOil.Services.Board;

public sealed class BoardService(
    IBoardRepository boardRepository,
    IColumnRepository columnRepository,
    ICardRepository cardRepository,
    IDbContextScopeFactory scopeFactory) : IBoardService
{
    public async Task<ApiResult<BoardDto>> GetBoardAsync()
    {
        using var scope = scopeFactory.CreateReadOnly();

        var board = await boardRepository.GetPrimaryBoardAsync();

        if (board is null)
        {
            return ApiErrors.InternalError("No board exists. Bootstrap has not run.");
        }

        var columns = await columnRepository.GetColumnsInBoardOrderedAsync(board.Id);

        var columnIds = columns.Select(x => x.Id).ToList();
        var cards = await cardRepository.GetCardsForColumnsOrderedAsync(columnIds);

        var cardsByColumnId = cards
            .GroupBy(x => x.BoardColumnId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<CardDto>)x
                    .Select(card => card.ToCardDto())
                    .ToList());

        var columnDtos = columns
            .Select(x => new BoardColumnDto(
                x.Id,
                x.Title,
                x.SortKey,
                x.CreatedAtUtc,
                x.UpdatedAtUtc,
                cardsByColumnId.GetValueOrDefault(x.Id, Array.Empty<CardDto>())))
            .ToList();

        return new BoardDto(
            board.Id,
            board.Name,
            board.CreatedAtUtc,
            board.UpdatedAtUtc,
            columnDtos);
    }
}
