namespace BoardOil.Abstractions.DataAccess;

public interface IRepositoryBase<TEntity>
    where TEntity : class
{
    IQueryable<TEntity> Query();
    void Add(TEntity entity);
    void AddRange(IEnumerable<TEntity> entities);
    void Remove(TEntity entity);
    void RemoveRange(IEnumerable<TEntity> entities);
}
