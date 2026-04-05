using BoardOil.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.CardType;
using BoardOil.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class CardTypeRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityCardType>(ambientDbContextLocator), ICardTypeRepository
{
    public Task<EntityCardType?> GetSystemByBoardIdAsync(int boardId) =>
        DbSet
            .Where(x => x.BoardId == boardId && x.IsSystem)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync();
}
