using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.Mcp;

public interface IMcpProjectConnectionRepository : IRepositoryBase<EntityMcpProjectConnection>
{
    Task<IReadOnlyList<EntityMcpProjectConnection>> GetAllOrderedAsync();
    Task<EntityMcpProjectConnection?> GetByIdWithClientAccountAsync(int id);
    Task<EntityMcpProjectConnection?> GetByPublicIdAsync(string publicId);
    Task<bool> AnyForClientAccountAsync(int clientAccountId);
    Task<bool> PublicIdExistsAsync(string publicId);
}
