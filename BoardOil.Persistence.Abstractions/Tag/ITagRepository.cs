using BoardOil.Persistence.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Persistence.Abstractions.Tag;

public interface ITagRepository : IRepositoryBase<EntityTag>
{
    Task<IReadOnlyList<EntityTag>> GetAllForBoardAsync(int boardId);
    Task<EntityTag?> GetByIdInBoardAsync(int boardId, int tagId);
    Task<EntityTag?> GetByNormalisedNameAsync(int boardId, string normalisedName);
}
