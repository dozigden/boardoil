using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Column;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Column;
using BoardOil.Services.Ordering;

namespace BoardOil.Services.Board;

public sealed class BoardBootstrapService(
    IBoardRepository boardRepository,
    IColumnRepository columnRepository) : IBoardBootstrapService
{
    public async Task EnsureDefaultBoardAsync()
    {
        var now = DateTime.UtcNow;
        var boardId = await boardRepository.GetPrimaryBoardIdAsync();
        if (boardId is null)
        {
            boardRepository.Add(new BoardCreateRecord(
                Name: "BoardOil",
                CreatedAtUtc: now,
                UpdatedAtUtc: now));

            await boardRepository.SaveChangesAsync();
            boardId = await boardRepository.GetPrimaryBoardIdAsync();
        }

        if (boardId is null)
        {
            return;
        }

        var existingColumns = await columnRepository.GetColumnsInBoardOrderedAsync(boardId.Value);
        if (existingColumns.Count > 0)
        {
            return;
        }

        var seedTitles = new[] { "Todo", "In Progress", "Done" };
        string? previousSortKey = null;
        foreach (var title in seedTitles)
        {
            var sortKey = SortKeyGenerator.Between(previousSortKey, null);
            await columnRepository.CreateAsync(new CreateColumnRecord(
                BoardId: boardId.Value,
                Title: title,
                SortKey: sortKey,
                CreatedAtUtc: now,
                UpdatedAtUtc: now));
            previousSortKey = sortKey;
        }
    }
}
