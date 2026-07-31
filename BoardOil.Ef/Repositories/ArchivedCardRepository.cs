using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Card;
using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class ArchivedCardRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityArchivedCard>(ambientDbContextLocator), IArchivedCardRepository
{
    public async Task<IReadOnlyList<EntityArchivedCard>> ListByBoardAsync(int boardId, string? normalisedSearch, int offset, int limit)
    {
        var query = BuildQuery(boardId, normalisedSearch);

        return await query
            .OrderByDescending(x => x.ArchivedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();
    }

    public Task<int> CountByBoardAsync(int boardId, string? normalisedSearch) =>
        BuildQuery(boardId, normalisedSearch).CountAsync();

    public async Task<IReadOnlyList<EntityArchivedCard>> ListForExportAsync(int boardId) =>
        await DbSet
            .AsNoTracking()
            .Where(x => x.BoardId == boardId)
            .OrderByDescending(x => x.ArchivedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

    public Task<EntityArchivedCard?> GetByBoardCardIdAsync(int boardId, int boardCardId) =>
        DbSet
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.BoardId == boardId && x.OriginalCardId == boardCardId);

    public Task<EntityArchivedCard?> GetByBoardCardIdForUpdateAsync(int boardId, int boardCardId) =>
        DbSet
            .SingleOrDefaultAsync(x => x.BoardId == boardId && x.OriginalCardId == boardCardId);

    private IQueryable<EntityArchivedCard> BuildQuery(int boardId, string? normalisedSearch)
    {
        var query = DbSet
            .AsNoTracking()
            .Where(x => x.BoardId == boardId);
        if (!string.IsNullOrWhiteSpace(normalisedSearch))
        {
            query = query.Where(x => x.SearchTextNormalised.Contains(normalisedSearch));
        }

        return query;
    }
}
