using BoardOil.Ef.Entities;

namespace BoardOil.Services.Abstractions;

public interface IBoardRepository
{
    Task<Board?> GetPrimaryBoardAsync();
    Task<int?> GetPrimaryBoardIdAsync();
    Task<bool> AnyBoardAsync();
    void Add(Board board);
    Task SaveChangesAsync();
}
