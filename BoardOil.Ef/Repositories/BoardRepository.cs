using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Board;
using BoardOil.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using BoardEntity = BoardOil.Persistence.Abstractions.Entities.EntityBoard;

namespace BoardOil.Ef.Repositories;

public sealed class BoardRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase(ambientDbContextLocator), IBoardRepository
{
    public Task<BoardRecord?> GetPrimaryBoardAsync() =>
        DbContext.Boards
            .OrderBy(x => x.Id)
            .Select(x => new BoardRecord(
                x.Id,
                x.Name,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .FirstOrDefaultAsync();

    public Task<int?> GetPrimaryBoardIdAsync() =>
        DbContext.Boards
            .OrderBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

    public Task<bool> AnyBoardAsync() =>
        DbContext.Boards.AnyAsync();

    public void Add(BoardCreateRecord board) =>
        DbContext.Boards.Add(new BoardEntity
        {
            Name = board.Name,
            CreatedAtUtc = board.CreatedAtUtc,
            UpdatedAtUtc = board.UpdatedAtUtc
        });
}
