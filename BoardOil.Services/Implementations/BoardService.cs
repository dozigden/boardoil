using BoardOil.Ef;
using BoardOil.Services.Abstractions;
using BoardOil.Services.Contracts;
using BoardOil.Services.Mappings;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Services.Implementations;

public sealed class BoardService(BoardOilDbContext dbContext) : IBoardService
{
    public async Task<ApiResult<BoardDto>> GetBoardAsync()
    {
        var board = await dbContext.Boards
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync();

        if (board is null)
        {
            return ApiErrors.InternalError("No board exists. Bootstrap has not run.");
        }

        var columns = await dbContext.Columns
            .Where(x => x.BoardId == board.Id)
            .OrderBy(x => x.Position)
            .ToListAsync();

        var columnIds = columns.Select(x => x.Id).ToList();
        var cards = await dbContext.Cards
            .Where(x => columnIds.Contains(x.BoardColumnId))
            .OrderBy(x => x.SortKey)
            .ToListAsync();

        var cardsByColumnId = cards
            .GroupBy(x => x.BoardColumnId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<CardDto>)x
                    .Select((card, index) => card.ToCardDto(index))
                    .ToList());

        var columnDtos = columns
            .Select(x => new BoardColumnDto(
                x.Id,
                x.Title,
                x.Position,
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
