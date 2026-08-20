using BoardOil.Contracts.Common;
using BoardOil.Contracts.Style;
using BoardOil.Contracts.Tag;

namespace BoardOil.Abstractions.Tag;

public interface ITagService
{
    Task<ApiResult<IReadOnlyList<TagDto>>> GetTagsAsync(int boardId, int actorUserId);
    Task<ApiResult<StyleDefaultDto>> GetCreateDefaultStyleAsync(int boardId, int actorUserId);
    Task<ApiResult<TagDto>> CreateTagAsync(int boardId, CreateTagRequest request, int actorUserId);
    Task<ApiResult<TagDto>> UpdateTagStyleAsync(int boardId, int tagId, UpdateTagRequest request, int actorUserId);
    Task<ApiResult> DeleteTagAsync(int boardId, int tagId, int actorUserId);
}
