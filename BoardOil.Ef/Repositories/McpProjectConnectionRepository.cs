using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Mcp;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class McpProjectConnectionRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityMcpProjectConnection>(ambientDbContextLocator), IMcpProjectConnectionRepository
{
    public async Task<IReadOnlyList<EntityMcpProjectConnection>> GetAllOrderedAsync() =>
        await DbSet
            .Include(x => x.ClientAccount)
            .OrderBy(x => x.RevokedAtUtc != null)
            .ThenBy(x => x.Name)
            .ThenBy(x => x.Id)
            .ToListAsync();

    public Task<EntityMcpProjectConnection?> GetByIdWithClientAccountAsync(int id) =>
        DbSet
            .Include(x => x.ClientAccount)
            .SingleOrDefaultAsync(x => x.Id == id);

    public Task<EntityMcpProjectConnection?> GetByPublicIdAsync(string publicId) =>
        DbSet
            .Include(x => x.ClientAccount)
            .SingleOrDefaultAsync(x => x.PublicId == publicId);

    public Task<bool> AnyForClientAccountAsync(int clientAccountId) =>
        DbSet.AnyAsync(x => x.ClientAccountId == clientAccountId);

    public Task<bool> PublicIdExistsAsync(string publicId) =>
        DbSet.AnyAsync(x => x.PublicId == publicId);
}
