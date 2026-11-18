using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class BoardRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityBoard>(ambientDbContextLocator), IBoardRepository
{
    public override EntityBoard? Get(int id) =>
        DbSet.SingleOrDefault(x => x.Id == id && x.DeletedAtUtc == null);

    public async Task<IReadOnlyList<int>> GetBoardIdsIncludingDeletedAsync(CancellationToken cancellationToken = default) =>
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
            .Where(x => x.DeletedAtUtc == null)
            .OrderBy(x => x.Id)
            .ToListAsync();

    public async Task<IReadOnlyList<EntityBoard>> GetDeletedBoardsOrderedAsync() =>
        await DbSet
            .Where(x => x.DeletedAtUtc != null)
            .OrderByDescending(x => x.DeletedAtUtc)
            .ThenBy(x => x.Id)
            .ToListAsync();

    public Task<EntityBoard?> GetIncludingDeletedAsync(int boardId) =>
        DbSet.SingleOrDefaultAsync(x => x.Id == boardId);

    public Task<bool> AnyBoardIncludingDeletedAsync() =>
        DbSet.AnyAsync();
}
