using BoardOil.Abstractions.DataAccess;

namespace BoardOil.Ef.Repositories;

public abstract class RepositoryBase(IAmbientDbContextLocator ambientDbContextLocator)
{
    private readonly IAmbientDbContextLocator _ambientDbContextLocator = ambientDbContextLocator;

    protected BoardOilDbContext DbContext
    {
        get
        {
            var dbContext = _ambientDbContextLocator.Get<BoardOilDbContext>();
            if (dbContext == null)
            {
                throw new ArgumentNullException(
                    "No ambient DbContext. Wrap data access in IDbContextScopeFactory.Create()/CreateReadOnly().");
            }

            return dbContext;
        }
    }
}
