using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Board;
using BoardOil.Persistence.Abstractions.Column;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Services.Ordering;

namespace BoardOil.Services.Board;

public sealed class BoardBootstrapService(
    IBoardRepository boardRepository,
    IColumnRepository columnRepository,
    IDbContextScopeFactory scopeFactory) : IBoardBootstrapService
{
    public async Task EnsureDefaultBoardAsync()
    {
        using var scope = scopeFactory.Create();
        if (await boardRepository.AnyBoardAsync())
        {
            return;
        }

        var now = DateTime.UtcNow;
        var board = new EntityBoard
        {
            Name = "BoardOil",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        boardRepository.Add(board);

        var seedTitles = new[] { "Todo", "In Progress", "Done" };
        string? previousSortKey = null;
        foreach (var title in seedTitles)
        {
            var sortKey = SortKeyGenerator.Between(previousSortKey, null);
            columnRepository.Add(new EntityBoardColumn
            {
                Board = board,
                Title = title,
                SortKey = sortKey,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            previousSortKey = sortKey;
        }

        await scope.SaveChangesAsync();
    }
}
