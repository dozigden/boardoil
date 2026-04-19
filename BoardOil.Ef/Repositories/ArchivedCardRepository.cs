using BoardOil.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Card;
using BoardOil.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class ArchivedCardRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityArchivedCard>(ambientDbContextLocator), IArchivedCardRepository
{
    public async Task<IReadOnlyList<EntityArchivedCard>> ListByBoardAsync(int boardId, string? normalisedSearch)
    {
        var query = DbSet
            .AsNoTracking()
            .Where(x => x.BoardId == boardId);
        if (!string.IsNullOrWhiteSpace(normalisedSearch))
        {
            query = query.Where(x => x.SearchTextNormalised.Contains(normalisedSearch));
        }

        return await query
            .OrderByDescending(x => x.ArchivedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
    }
}
