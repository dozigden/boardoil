using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;

namespace BoardOil.Abstractions.Card;

public interface ICardCommentService
{
    Task<ApiResult<IReadOnlyList<CardCommentDto>>> GetCommentsAsync(int boardId, int cardId, int actorUserId);
    Task<ApiResult<CardCommentDto>> CreateCommentAsync(int boardId, int cardId, CreateCardCommentRequest request, int actorUserId);
    Task<ApiResult<CardCommentDto>> UpdateCommentAsync(int boardId, int cardId, int commentId, UpdateCardCommentRequest request, int actorUserId);
    Task<ApiResult> DeleteCommentAsync(int boardId, int cardId, int commentId, int actorUserId);
}
