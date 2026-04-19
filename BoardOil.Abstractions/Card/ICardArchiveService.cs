using BoardOil.Contracts.Card;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Abstractions.Card;

public interface ICardArchiveService
{
    Task<ApiResult<IReadOnlyList<ArchivedCardDto>>> GetArchivedCardsAsync(int boardId, string? search, int actorUserId);
    Task<ApiResult<ArchivedCardDto>> ArchiveCardAsync(int boardId, int id, int actorUserId);
}
