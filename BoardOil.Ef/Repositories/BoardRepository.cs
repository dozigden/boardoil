using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class BoardRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityBoard>(ambientDbContextLocator), IBoardRepository
{
    public async Task<IReadOnlyList<int>> GetBoardIdsAsync(CancellationToken cancellationToken = default) =>
        await DbSet.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(cancellationToken);

    public Task<EntityBoard?> GetForResaveAsync(int boardId, CancellationToken cancellationToken = default) =>
        DbSet.AsSplitQuery()
            .Include(x => x.Columns).ThenInclude(x => x.Cards).ThenInclude(x => x.CardTags)
            .Include(x => x.CardTypes)
            .Include(x => x.Tags)
            .Include(x => x.Slicks)
            .SingleOrDefaultAsync(x => x.Id == boardId, cancellationToken);

    public async Task<IReadOnlyList<EntityBoard>> GetBoardsOrderedAsync() =>
        await DbSet
            .OrderBy(x => x.Id)
            .ToListAsync();

    public async Task<IReadOnlyList<EntityBoard>> GetBoardsForUserOrderedAsync(int userId) =>
        await DbContext.BoardMembers
            .Where(x => x.UserId == userId)
            .Select(x => x.Board)
            .OrderBy(x => x.Id)
            .ToListAsync();

    public async Task<IReadOnlyList<EntityBoard>> GetBoardsByIdsOrderedAsync(IReadOnlyList<int> boardIds)
    {
        if (boardIds.Count == 0)
        {
            return Array.Empty<EntityBoard>();
        }

        return await DbSet
            .Where(x => boardIds.Contains(x.Id))
            .OrderBy(x => x.Id)
            .ToListAsync();
    }

    public Task<bool> AnyBoardAsync() =>
        DbSet.AnyAsync();
}
