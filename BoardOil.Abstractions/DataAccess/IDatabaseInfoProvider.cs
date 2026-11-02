using BoardOil.Contracts.Support;

namespace BoardOil.Abstractions.DataAccess;

public interface IDatabaseInfoProvider
{
    Task<DatabaseInfoDto> GetAsync(CancellationToken cancellationToken = default);
}
