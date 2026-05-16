using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Slick;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class SlickRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntitySlick>(ambientDbContextLocator), ISlickRepository
{
    public async Task<IReadOnlyList<EntitySlick>> GetAllForBoardAsync(int boardId) =>
        await DbSet
            .Where(x => x.BoardId == boardId)
            .OrderBy(x => x.Name)
            .ToListAsync();

    public Task<EntitySlick?> GetByIdInBoardAsync(int boardId, int slickId) =>
        DbSet
            .Where(x => x.BoardId == boardId && x.Id == slickId)
            .FirstOrDefaultAsync();

    public Task<EntitySlick?> GetByNormalisedNameAsync(int boardId, string normalisedName) =>
        DbSet
            .Where(x => x.BoardId == boardId && x.NormalisedName == normalisedName)
            .FirstOrDefaultAsync();
}
