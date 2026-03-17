using BoardOil.Ef;
using BoardOil.Ef.Entities;
using Microsoft.EntityFrameworkCore;
using BoardEntity = BoardOil.Ef.Entities.Board;

namespace BoardOil.Services.Board;

public sealed class BoardRepository(BoardOilDbContext dbContext) : IBoardRepository
{
    public Task<BoardEntity?> GetPrimaryBoardAsync() =>
        dbContext.Boards
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync();

    public Task<int?> GetPrimaryBoardIdAsync() =>
        dbContext.Boards
            .OrderBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

    public Task<bool> AnyBoardAsync() =>
        dbContext.Boards.AnyAsync();

    public void Add(BoardEntity board) =>
        dbContext.Boards.Add(board);

    public Task SaveChangesAsync() =>
        dbContext.SaveChangesAsync();
}
