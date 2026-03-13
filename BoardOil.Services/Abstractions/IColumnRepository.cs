using BoardOil.Ef.Entities;

namespace BoardOil.Services.Abstractions;

public interface IColumnRepository
{
    Task<IReadOnlyList<BoardColumn>> GetColumnsInBoardOrderedAsync(int boardId);
    void Add(BoardColumn column);
    void Remove(BoardColumn column);
    Task SaveChangesAsync();
}
