using BoardOil.Ef.Entities;
using BoardEntity = BoardOil.Ef.Entities.Board;

namespace BoardOil.Services.Board;

public interface IBoardRepository
{
    Task<BoardEntity?> GetPrimaryBoardAsync();
    Task<int?> GetPrimaryBoardIdAsync();
    Task<bool> AnyBoardAsync();
    void Add(BoardEntity board);
    Task SaveChangesAsync();
}
