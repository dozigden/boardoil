using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.Tag;

public interface ITagRepository : IRepositoryBase<EntityTag>
{
    Task<IReadOnlyList<EntityTag>> GetAllForBoardAsync(int boardId);
    Task<EntityTag?> GetByIdInBoardAsync(int boardId, int tagId);
    Task<EntityTag?> GetByNormalisedNameAsync(int boardId, string normalisedName);
}
