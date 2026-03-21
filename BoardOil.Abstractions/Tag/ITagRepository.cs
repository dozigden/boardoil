using BoardOil.Contracts.Tag;

namespace BoardOil.Abstractions.Tag;

public interface ITagRepository
{
    Task<IReadOnlyList<TagRecord>> GetAllAsync();
    Task<TagRecord?> GetByNormalisedNameAsync(string normalisedName);
    Task<TagRecord> CreateAsync(CreateTagRecord tag);
    Task<bool> UpdateAsync(UpdateTagRecord tag);
    Task<bool> DeleteAsync(string normalisedName);
}
