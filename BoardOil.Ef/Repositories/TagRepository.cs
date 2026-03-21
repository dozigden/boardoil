using BoardOil.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Persistence.Abstractions.Tag;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class TagRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityTag>(ambientDbContextLocator), ITagRepository
{
    public async Task<IReadOnlyList<EntityTag>> GetAllAsync() =>
        await DbSet
            .OrderBy(x => x.Name)
            .ToListAsync();

    public Task<EntityTag?> GetByNormalisedNameAsync(string normalisedName) =>
        DbSet
            .Where(x => x.NormalisedName == normalisedName)
            .FirstOrDefaultAsync();
}
