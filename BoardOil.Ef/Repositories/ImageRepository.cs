using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Image;
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

    public async Task<IReadOnlyList<EntityImage>> GetLatestForEntitiesAsync(ImageEntityType entityType, IReadOnlyCollection<int> entityIds)
    {
        if (entityIds.Count == 0)
        {
            return [];
        }

        return await Query()
            .Where(x => x.EntityType == entityType && entityIds.Contains(x.EntityId))
            .GroupBy(x => x.EntityId)
            .Select(g => g
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .First())
            .ToListAsync();
    }
}
