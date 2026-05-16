using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.Image;

public interface IImageRepository : IRepositoryBase<EntityImage>
{
    Task<EntityImage?> GetLatestForEntityAsync(ImageEntityType entityType, int entityId);
    Task<IReadOnlyList<EntityImage>> GetLatestForEntitiesAsync(ImageEntityType entityType, IReadOnlyCollection<int> entityIds);
}
