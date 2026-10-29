using System.Text.RegularExpressions;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.ErrorLogs;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class ErrorLogRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityErrorLog>(ambientDbContextLocator), IErrorLogRepository
{
    public Task<int> CountAsync(ErrorLogFilter filter, CancellationToken cancellationToken = default) =>
        Filter(filter).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<EntityErrorLog>> ListAsync(int offset, int limit, ErrorLogFilter filter, CancellationToken cancellationToken = default) =>
        await Filter(filter)
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

    private IQueryable<EntityErrorLog> Filter(ErrorLogFilter filter)
    {
        var query = DbSet.AsNoTracking();
        if (filter.FromUtc is { } fromUtc) query = query.Where(x => x.OccurredAtUtc >= fromUtc);
        if (filter.ToUtc is { } toUtc) query = query.Where(x => x.OccurredAtUtc < toUtc);
        if (filter.Source is { } source) query = query.Where(x => x.Source == source);
        if (filter.Area is { } area) query = query.Where(x => x.Area == area);
        if (filter.TraceIdentifier is { } trace) query = query.Where(x => x.TraceIdentifier == trace);
        if (filter.Message is { } message)
        {
            // Match live card search: REGEXP uses .NET Unicode casing and escaped literal input.
            var pattern = "(?i)" + Regex.Escape(message);
            query = query.Where(x => Regex.IsMatch(x.Message, pattern));
        }
        return query;
    }

    public Task<EntityErrorLog?> GetAsync(int id) =>
        DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

    public Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default) =>
        DbSet
            .Where(x => x.OccurredAtUtc < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);
}
