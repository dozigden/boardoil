using BoardOil.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Card;
using BoardOil.Persistence.Abstractions.Entities;
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

    public Task<EntityArchivedCard?> GetByIdAsync(int boardId, int archivedCardId) =>
        DbSet
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.BoardId == boardId && x.Id == archivedCardId);

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
