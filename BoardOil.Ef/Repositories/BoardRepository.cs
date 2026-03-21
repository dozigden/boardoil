using BoardOil.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Board;
using BoardOil.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class BoardRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityBoard>(ambientDbContextLocator), IBoardRepository
{
    public Task<EntityBoard?> GetPrimaryBoardAsync() =>
        DbSet
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync();

    public Task<int?> GetPrimaryBoardIdAsync() =>
        DbSet
            .OrderBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

    public Task<bool> AnyBoardAsync() =>
        DbSet.AnyAsync();
}
