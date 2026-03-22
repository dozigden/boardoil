using BoardOil.Contracts.Contracts;
using BoardOil.Contracts.Tag;

namespace BoardOil.Abstractions.Tag;

public interface ITagService
{
    Task<ApiResult<IReadOnlyList<TagDto>>> GetTagsAsync();
    Task<ApiResult<TagDto>> CreateTagAsync(CreateTagRequest request);
    Task<ApiResult<TagDto>> UpdateTagStyleAsync(int tagId, UpdateTagStyleRequest request);
}
