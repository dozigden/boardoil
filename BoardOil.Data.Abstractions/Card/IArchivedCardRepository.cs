using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.Card;

public interface IArchivedCardRepository : IRepositoryBase<EntityArchivedCard>
{
    Task<IReadOnlyList<EntityArchivedCard>> ListByBoardAsync(int boardId, string? normalisedSearch, int offset, int limit);
    Task<IReadOnlyList<EntityArchivedCard>> ListForExportAsync(int boardId);
    Task<IReadOnlyList<int>> ListExistingOriginalCardIdsAsync(IReadOnlyList<int> originalCardIds);
    Task<int?> GetMinimumOriginalCardIdAsync();
    Task<int> CountByBoardAsync(int boardId, string? normalisedSearch);
    Task<EntityArchivedCard?> GetByBoardCardIdAsync(int boardId, int boardCardId);
    Task<EntityArchivedCard?> GetByBoardCardIdForUpdateAsync(int boardId, int boardCardId);
}
