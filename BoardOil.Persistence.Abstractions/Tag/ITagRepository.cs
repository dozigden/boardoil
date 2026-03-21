using BoardOil.Persistence.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Persistence.Abstractions.Tag;

public interface ITagRepository : IRepositoryBase<EntityTag>
{
    Task<IReadOnlyList<EntityTag>> GetAllAsync();
    Task<EntityTag?> GetByNormalisedNameAsync(string normalisedName);
}
