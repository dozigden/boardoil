using BoardOil.Contracts.Column;

namespace BoardOil.Abstractions.Column;

public interface IColumnRepository
{
    Task<IReadOnlyList<ColumnRecord>> GetColumnsInBoardOrderedAsync(int boardId);
    void Add(CreateColumnRecord column);
    Task UpdateAsync(UpdateColumnRecord column);
    Task DeleteAsync(int id);
}
