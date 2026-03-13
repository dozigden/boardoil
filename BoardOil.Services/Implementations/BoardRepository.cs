using BoardOil.Ef;
using BoardOil.Ef.Entities;
using BoardOil.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Services.Implementations;

public sealed class BoardRepository(BoardOilDbContext dbContext) : IBoardRepository
{
    public Task<Board?> GetPrimaryBoardAsync() =>
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

    public void Add(Board board) =>
        dbContext.Boards.Add(board);

    public Task SaveChangesAsync() =>
        dbContext.SaveChangesAsync();
}
