using BoardOil.Abstractions.DataAccess;
using Microsoft.EntityFrameworkCore;

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

public abstract class RepositoryBase<TEntity>(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase(ambientDbContextLocator), IRepositoryBase<TEntity>
    where TEntity : class
{
    protected DbSet<TEntity> DbSet => DbContext.Set<TEntity>();

    public virtual IQueryable<TEntity> Query() => DbSet;

    public virtual void Add(TEntity entity) => DbSet.Add(entity);

    public virtual void AddRange(IEnumerable<TEntity> entities) => DbSet.AddRange(entities);

    public virtual void Remove(TEntity entity) => DbSet.Remove(entity);

    public virtual void RemoveRange(IEnumerable<TEntity> entities) => DbSet.RemoveRange(entities);
}
