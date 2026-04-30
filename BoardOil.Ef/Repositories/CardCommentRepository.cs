using BoardOil.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Card;
using BoardOil.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class CardCommentRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityCardComment>(ambientDbContextLocator), ICardCommentRepository
{
    public async Task<IReadOnlyList<EntityCardComment>> GetForCardOrderedAsync(int cardId) =>
        await DbSet
            .Where(x => x.CardId == cardId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
}
