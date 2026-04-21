using BoardOil.Contracts.Card;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Abstractions.Card;

public interface ICardArchiveService
{
    Task<ApiResult<ArchivedCardListDto>> GetArchivedCardsAsync(int boardId, string? search, int? offset, int? limit, int actorUserId);
    Task<ApiResult<ArchivedCardDetailDto>> GetArchivedCardAsync(int boardId, int archivedCardId, int actorUserId);
    Task<ApiResult<ArchivedCardDto>> ArchiveCardAsync(int boardId, int id, int actorUserId);
    Task<ApiResult<ArchiveCardsSummaryDto>> ArchiveCardsAsync(int boardId, ArchiveCardsRequest request, int actorUserId);
}
