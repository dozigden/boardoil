using BoardOil.Contracts.CustomField;
using BoardOil.Contracts.Common;

namespace BoardOil.Abstractions.CustomField;

public interface ICustomFieldService
{
    Task<ApiResult<IReadOnlyList<CustomFieldDto>>> GetAsync(int boardId, int actorUserId);
    Task<ApiResult<IReadOnlyList<CustomFieldDto>>> CreateAsync(int boardId, CustomFieldRequest input, int actorUserId);
    Task<ApiResult<IReadOnlyList<CustomFieldDto>>> UpdateAsync(int boardId, int customFieldId, CustomFieldRequest input, int actorUserId);
    Task<ApiResult<IReadOnlyList<CustomFieldDto>>> DeleteAsync(int boardId, int customFieldId, int actorUserId);
    Task<ApiResult<IReadOnlyList<CustomFieldDto>>> ReorderAsync(int boardId, CustomFieldOrderRequest request, int actorUserId);
    Task<ApiResult<CustomFieldPreviewDto>> PreviewAsync(int boardId, int customFieldId, CustomFieldPreviewRequest request, int actorUserId);
}
