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
            .Include(x => x.AuthorUser)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

    public async Task<IReadOnlyList<EntityCardComment>> GetForCardsOrderedAsync(IReadOnlyList<int> cardIds)
    {
        if (cardIds.Count == 0)
        {
            return [];
        }

        return await DbSet
            .Where(x => cardIds.Contains(x.CardId))
            .Include(x => x.AuthorUser)
            .OrderBy(x => x.CardId)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
    }

    public Task<EntityCardComment?> GetByIdWithAuthorAsync(int id) =>
        DbSet
            .Include(x => x.AuthorUser)
            .FirstOrDefaultAsync(x => x.Id == id);
}
