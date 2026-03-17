using BoardOil.Abstractions.Board;
using BoardOil.Contracts.Board;
using BoardOil.Ef;
using BoardOil.Ef.Entities;
using Microsoft.EntityFrameworkCore;
using BoardEntity = BoardOil.Ef.Entities.Board;

namespace BoardOil.Ef.Repositories;

public sealed class BoardRepository(BoardOilDbContext dbContext) : IBoardRepository
{
    public Task<BoardRecord?> GetPrimaryBoardAsync() =>
        dbContext.Boards
            .OrderBy(x => x.Id)
            .Select(x => new BoardRecord(
                x.Id,
                x.Name,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .FirstOrDefaultAsync();

    public Task<int?> GetPrimaryBoardIdAsync() =>
        dbContext.Boards
            .OrderBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

    public Task<bool> AnyBoardAsync() =>
        dbContext.Boards.AnyAsync();

    public void Add(BoardCreateRecord board) =>
        dbContext.Boards.Add(new BoardEntity
        {
            Name = board.Name,
            CreatedAtUtc = board.CreatedAtUtc,
            UpdatedAtUtc = board.UpdatedAtUtc
        });

    public Task SaveChangesAsync() =>
        dbContext.SaveChangesAsync();
}
