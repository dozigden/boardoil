using BoardOil.Persistence.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Persistence.Abstractions.Image;

public interface IImageRepository : IRepositoryBase<EntityImage>
{
    Task<EntityImage?> GetLatestForEntityAsync(ImageEntityType entityType, int entityId);
}
