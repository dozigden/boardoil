using BoardOil.Contracts.CustomField;
using BoardOil.Contracts.Common;

namespace BoardOil.Abstractions.CustomField;

public interface ICustomFieldService
{
    Task<ApiResult<IReadOnlyList<CustomFieldDto>>> GetAsync(int boardId, int actorUserId);
    Task<ApiResult<IReadOnlyList<CustomFieldDto>>> CreateAsync(int boardId, CustomFieldInput input, int actorUserId);
    Task<ApiResult<IReadOnlyList<CustomFieldDto>>> UpdateAsync(int boardId, string code, CustomFieldInput input, int actorUserId);
    Task<ApiResult<IReadOnlyList<CustomFieldDto>>> DeleteAsync(int boardId, string code, int actorUserId);
    Task<ApiResult<IReadOnlyList<CustomFieldDto>>> ReorderAsync(int boardId, CustomFieldOrderRequest request, int actorUserId);
    Task<ApiResult<CustomFieldPreviewDto>> PreviewAsync(int boardId, string code, CustomFieldPreviewRequest request, int actorUserId);
}
