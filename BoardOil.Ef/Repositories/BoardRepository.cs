using BoardOil.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Board;
using BoardOil.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class BoardRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityBoard>(ambientDbContextLocator), IBoardRepository
{
    public async Task<IReadOnlyList<EntityBoard>> GetBoardsOrderedAsync() =>
        await DbSet
            .OrderBy(x => x.Id)
            .ToListAsync();

    public Task<bool> AnyBoardAsync() =>
        DbSet.AnyAsync();
}
