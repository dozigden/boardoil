using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.ErrorLogs;

public interface IErrorLogRepository : IRepositoryBase<EntityErrorLog>
{
    Task<int> CountAsync(ErrorLogFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EntityErrorLog>> ListAsync(int offset, int limit, ErrorLogFilter filter, CancellationToken cancellationToken = default);
    Task<EntityErrorLog?> GetAsync(int id);
    Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
}
