using BoardOil.Ef;
using BoardOil.Ef.Entities;
using BoardOil.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Services.Implementations;

public sealed class ColumnRepository(BoardOilDbContext dbContext) : IColumnRepository
{
    public async Task<IReadOnlyList<BoardColumn>> GetColumnsInBoardOrderedAsync(int boardId) =>
        await dbContext.Columns
            .Where(x => x.BoardId == boardId)
            .OrderBy(x => x.Position)
            .ToListAsync();

    public void Add(BoardColumn column) =>
        dbContext.Columns.Add(column);

    public void Remove(BoardColumn column) =>
        dbContext.Columns.Remove(column);

    public Task SaveChangesAsync() =>
        dbContext.SaveChangesAsync();
}
