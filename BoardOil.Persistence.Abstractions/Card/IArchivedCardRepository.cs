using BoardOil.Persistence.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Persistence.Abstractions.Card;

public interface IArchivedCardRepository : IRepositoryBase<EntityArchivedCard>
{
    Task<IReadOnlyList<EntityArchivedCard>> ListByBoardAsync(int boardId, string? normalisedSearch, int offset, int limit);
    Task<IReadOnlyList<EntityArchivedCard>> ListForExportAsync(int boardId);
    Task<IReadOnlyList<int>> ListExistingOriginalCardIdsAsync(IReadOnlyList<int> originalCardIds);
    Task<int?> GetMinimumOriginalCardIdAsync();
    Task<int> CountByBoardAsync(int boardId, string? normalisedSearch);
    Task<EntityArchivedCard?> GetByIdAsync(int boardId, int archivedCardId);
}
