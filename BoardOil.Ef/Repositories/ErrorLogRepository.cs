using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.ErrorLogs;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class ErrorLogRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityErrorLog>(ambientDbContextLocator), IErrorLogRepository
{
    public Task<int> CountAsync() =>
        DbSet.AsNoTracking().CountAsync();

    public async Task<IReadOnlyList<EntityErrorLog>> ListAsync(int offset, int limit) =>
        await DbSet
            .AsNoTracking()
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

    public Task<EntityErrorLog?> GetAsync(int id) =>
        DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

    public Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default) =>
        DbSet
            .Where(x => x.OccurredAtUtc < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);
}
