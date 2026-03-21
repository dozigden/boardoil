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

        await scope.Transaction(async (transactionScope, transaction) =>
        {
            var now = DateTime.UtcNow;
            var boardId = await boardRepository.GetPrimaryBoardIdAsync();
            if (boardId is null)
            {
                boardRepository.Add(new EntityBoard
                {
                    Name = "BoardOil",
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });

                await transactionScope.SaveChangesAsync();
                boardId = await boardRepository.GetPrimaryBoardIdAsync();
            }

            if (boardId is null)
            {
                await transaction.CommitAsync();
                return;
            }

            var existingColumns = await columnRepository.GetColumnsInBoardOrderedAsync(boardId.Value);
            if (existingColumns.Count == 0)
            {
                var seedTitles = new[] { "Todo", "In Progress", "Done" };
                string? previousSortKey = null;
                foreach (var title in seedTitles)
                {
                    var sortKey = SortKeyGenerator.Between(previousSortKey, null);
                    columnRepository.Add(new EntityBoardColumn
                    {
                        BoardId = boardId.Value,
                        Title = title,
                        SortKey = sortKey,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    });
                    previousSortKey = sortKey;
                }

                await transactionScope.SaveChangesAsync();
            }

            await transaction.CommitAsync();
        });
    }
}
