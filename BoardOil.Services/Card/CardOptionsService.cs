using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.CardType;
using BoardOil.Abstractions.Column;
using BoardOil.Abstractions.Slick;
using BoardOil.Abstractions.Tag;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;

namespace BoardOil.Services.Card;

public sealed class CardOptionsService(
    IColumnService columnService,
    IBoardMemberService boardMemberService,
    ICardTypeService cardTypeService,
    ITagService tagService,
    ISlickService slickService) : ICardOptionsService
{
    public async Task<ApiResult<BoardCardOptionsDto>> GetOptionsAsync(int boardId, int actorUserId)
    {
        var columnsResult = await columnService.GetColumnsAsync(boardId, actorUserId);
        if (!columnsResult.Success || columnsResult.Data is null)
        {
            return CopyFailure(columnsResult);
        }

        var membersResult = await boardMemberService.GetActiveMembersAsync(boardId, actorUserId);
        if (!membersResult.Success || membersResult.Data is null)
        {
            return CopyFailure(membersResult);
        }

        var cardTypesResult = await cardTypeService.GetCardTypesAsync(boardId, actorUserId);
        if (!cardTypesResult.Success || cardTypesResult.Data is null)
        {
            return CopyFailure(cardTypesResult);
        }

        var defaultCardType = cardTypesResult.Data.SingleOrDefault(cardType => cardType.IsSystem);
        if (defaultCardType is null)
        {
            return ApiErrors.InternalError("Default card type not found for board.");
        }

        var tagsResult = await tagService.GetTagsAsync(boardId, actorUserId);
        if (!tagsResult.Success || tagsResult.Data is null)
        {
            return CopyFailure(tagsResult);
        }

        var slicksResult = await slickService.GetSlicksAsync(boardId, actorUserId);
        if (!slicksResult.Success || slicksResult.Data is null)
        {
            return CopyFailure(slicksResult);
        }

        return new BoardCardOptionsDto(
            boardId,
            columnsResult.Data,
            membersResult.Data,
            cardTypesResult.Data,
            defaultCardType.Id,
            tagsResult.Data,
            slicksResult.Data);
    }

    private static ApiResult<BoardCardOptionsDto> CopyFailure<T>(ApiResult<T> result) =>
        new(false, null, result.StatusCode, result.Message, result.ValidationErrors);
}
