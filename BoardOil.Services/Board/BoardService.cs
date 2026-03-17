using BoardOil.Services.Card;
using BoardOil.Services.Column;
using BoardOil.Services.Contracts;

namespace BoardOil.Services.Board;

public sealed class BoardService(IBoardRepository boardRepository, IColumnRepository columnRepository, ICardRepository cardRepository) : IBoardService
{
    public async Task<ApiResult<BoardDto>> GetBoardAsync()
    {
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
                    .Select((card, index) => card.ToCardDto(index))
                    .ToList());

        var columnDtos = columns
            .Select((x, index) => new BoardColumnDto(
                x.Id,
                x.Title,
                index,
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
