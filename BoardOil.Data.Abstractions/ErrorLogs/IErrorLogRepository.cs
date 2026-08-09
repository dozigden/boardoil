using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.ErrorLogs;

public interface IErrorLogRepository : IRepositoryBase<EntityErrorLog>
{
    Task<int> CountAsync();
    Task<IReadOnlyList<EntityErrorLog>> ListAsync(int offset, int limit);
    Task<EntityErrorLog?> GetAsync(int id);
    Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
}
