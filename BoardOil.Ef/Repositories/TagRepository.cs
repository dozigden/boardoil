using BoardOil.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Persistence.Abstractions.Tag;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class TagRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityTag>(ambientDbContextLocator), ITagRepository
{
    public async Task<IReadOnlyList<EntityTag>> GetAllForBoardAsync(int boardId) =>
        await DbSet
            .Where(x => x.BoardId == boardId)
            .OrderBy(x => x.Name)
            .ToListAsync();

    public Task<EntityTag?> GetByIdInBoardAsync(int boardId, int tagId) =>
        DbSet
            .Where(x => x.BoardId == boardId && x.Id == tagId)
            .FirstOrDefaultAsync();

    public Task<EntityTag?> GetByNormalisedNameAsync(int boardId, string normalisedName) =>
        DbSet
            .Where(x => x.BoardId == boardId && x.NormalisedName == normalisedName)
            .FirstOrDefaultAsync();
}
