using BoardOil.Contracts.Board;

namespace BoardOil.Abstractions.Board;

public interface IBoardRepository
{
    Task<BoardRecord?> GetPrimaryBoardAsync();
    Task<int?> GetPrimaryBoardIdAsync();
    Task<bool> AnyBoardAsync();
    void Add(BoardCreateRecord board);
}
