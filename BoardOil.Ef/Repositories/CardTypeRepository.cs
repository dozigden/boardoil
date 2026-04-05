using BoardOil.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.CardType;
using BoardOil.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class CardTypeRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityCardType>(ambientDbContextLocator), ICardTypeRepository
{
    public async Task<IReadOnlyList<EntityCardType>> GetAllForBoardAsync(int boardId) =>
        await DbSet
            .Where(x => x.BoardId == boardId)
            .OrderByDescending(x => x.IsSystem)
            .ThenBy(x => x.Name)
            .ToListAsync();

    public Task<EntityCardType?> GetByIdInBoardAsync(int boardId, int cardTypeId) =>
        DbSet
            .Where(x => x.BoardId == boardId && x.Id == cardTypeId)
            .FirstOrDefaultAsync();

    public Task<EntityCardType?> GetByNormalisedNameAsync(int boardId, string normalisedName) =>
        DbSet
            .Where(x => x.BoardId == boardId && x.Name.ToUpper() == normalisedName)
            .FirstOrDefaultAsync();

    public Task<EntityCardType?> GetSystemByBoardIdAsync(int boardId) =>
        DbSet
            .Where(x => x.BoardId == boardId && x.IsSystem)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync();
}
