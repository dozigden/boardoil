using BoardOil.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Card;
using BoardOil.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class ArchivedCardRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityArchivedCard>(ambientDbContextLocator), IArchivedCardRepository
{
    private const int MaxOriginalCardIdsPerQuery = 500;

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

    public async Task<IReadOnlyList<int>> ListExistingOriginalCardIdsAsync(IReadOnlyList<int> originalCardIds)
    {
        if (originalCardIds.Count == 0)
        {
            return [];
        }

        var uniqueOriginalCardIds = originalCardIds
            .Distinct()
            .ToArray();
        var existingOriginalCardIds = new HashSet<int>();

        foreach (var idChunk in uniqueOriginalCardIds.Chunk(MaxOriginalCardIdsPerQuery))
        {
            var chunk = idChunk.ToArray();
            var existingIdsInChunk = await DbSet
                .AsNoTracking()
                .Where(x => chunk.Contains(x.OriginalCardId))
                .Select(x => x.OriginalCardId)
                .Distinct()
                .ToListAsync();
            foreach (var existingId in existingIdsInChunk)
            {
                existingOriginalCardIds.Add(existingId);
            }
        }

        return existingOriginalCardIds.ToList();
    }

    public Task<int?> GetMinimumOriginalCardIdAsync() =>
        DbSet
            .AsNoTracking()
            .Select(x => (int?)x.OriginalCardId)
            .MinAsync();

    public Task<EntityArchivedCard?> GetByIdAsync(int boardId, int archivedCardId) =>
        DbSet
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.BoardId == boardId && x.Id == archivedCardId);

    public Task<EntityArchivedCard?> GetByIdForUpdateAsync(int boardId, int archivedCardId) =>
        DbSet
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
