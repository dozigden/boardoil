using BoardOil.Contracts.Card;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Abstractions.Card;

public interface ICardService
{
    Task<ApiResult<CardDto>> GetCardAsync(int boardId, int id, int actorUserId);
    Task<ApiResult<IReadOnlyList<ArchivedCardDto>>> GetArchivedCardsAsync(int boardId, string? search, int actorUserId);
    Task<ApiResult<CardDto>> CreateCardAsync(int boardId, CreateCardRequest request, int actorUserId);
    Task<ApiResult<CardDto>> UpdateCardAsync(int boardId, int id, UpdateCardRequest request, int actorUserId);
    Task<ApiResult<CardDto>> MoveCardAsync(int boardId, int id, MoveCardRequest request, int actorUserId);
    Task<ApiResult<ArchivedCardDto>> ArchiveCardAsync(int boardId, int id, int actorUserId);
    Task<ApiResult> DeleteCardAsync(int boardId, int id, int actorUserId);
}
