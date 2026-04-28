using BoardOil.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Persistence.Abstractions.Image;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class ImageRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityImage>(ambientDbContextLocator), IImageRepository
{
    public Task<EntityImage?> GetLatestForEntityAsync(ImageEntityType entityType, int entityId) =>
        Query()
            .Where(x => x.EntityType == entityType && x.EntityId == entityId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync();
}
