using BoardOil.Contracts.Board;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.Slick;
using BoardOil.Mcp.Contracts;

namespace BoardOil.Api.Mcp;

public static class McpMappingExtensions
{
    public static McpBoardSummary ToMcp(this BoardSummaryDto board) =>
        new(
            board.Id,
            board.Name,
            board.Description,
            board.CreatedAtUtc,
            board.UpdatedAtUtc,
            board.CurrentUserRole);

    public static McpBoardSnapshot ToMcp(this BoardDto board, IReadOnlyDictionary<int, McpCardSlickSnapshot>? slicksById = null) =>
        new(
            board.Id,
            board.Name,
            board.Description,
            board.UpdatedAtUtc,
            board.Columns
                .Select(column => new McpColumnSnapshot(
                    column.Id,
                    column.Title,
                    column.SortKey,
                    column.Cards.Select(card => card.ToMcpBoardSnapshot(slicksById)).ToArray()))
                .ToArray());

    public static McpCardSnapshot ToMcp(this CardDto card, IReadOnlyDictionary<int, McpCardSlickSnapshot>? slicksById = null) =>
        new(
            card.Id,
            card.BoardColumnId,
            card.CardTypeId,
            card.CardTypeName,
            card.CardTypeEmoji,
            card.Title,
            card.Description,
            card.SortKey,
            card.Tags.Select(tag => tag.ToMcp()).ToArray(),
            card.TagNames,
            card.UpdatedAtUtc,
            card.AssignedUserId,
            card.AssignedUserName,
            card.SlickId,
            ResolveSlickSnapshot(card.SlickId, slicksById),
            [],
            card.ExternalUrl);

    public static McpCardCommentSnapshot ToMcp(this CardCommentDto comment) =>
        new(
            comment.Id,
            comment.CardId,
            comment.AuthorUserId,
            comment.Text,
            comment.PostedAtUtc,
            comment.AuthorDisplayName,
            comment.AuthorImageRelativePath);

    public static McpCardSlickSnapshot ToMcp(this SlickDto slick) =>
        new(
            slick.Id,
            slick.Name,
            slick.StyleName,
            slick.StylePropertiesJson);

    private static McpBoardCardSnapshot ToMcpBoardSnapshot(this CardDto card, IReadOnlyDictionary<int, McpCardSlickSnapshot>? slicksById) =>
        new(
            card.Id,
            card.BoardColumnId,
            card.CardTypeId,
            card.CardTypeName,
            card.CardTypeEmoji,
            card.Title,
            card.SortKey,
            card.Tags.Select(tag => tag.ToMcp()).ToArray(),
            card.TagNames,
            card.UpdatedAtUtc,
            card.AssignedUserId,
            card.AssignedUserName,
            card.SlickId,
            ResolveSlickSnapshot(card.SlickId, slicksById),
            card.ExternalUrl);

    private static McpCardTagSnapshot ToMcp(this CardTagDto tag) =>
        new(
            tag.Id,
            tag.Name,
            tag.StyleName,
            tag.StylePropertiesJson,
            tag.Emoji);

    private static McpCardSlickSnapshot? ResolveSlickSnapshot(
        int? slickId,
        IReadOnlyDictionary<int, McpCardSlickSnapshot>? slicksById)
    {
        if (slickId is null || slicksById is null)
        {
            return null;
        }

        return slicksById.GetValueOrDefault(slickId.Value);
    }

    public static McpToolError ToMcpError(this ApiResult apiResult)
    {
        var code = apiResult.StatusCode switch
        {
            400 => "validation_failed",
            401 => "unauthorised",
            403 => "forbidden",
            404 => "not_found",
            _ => "service_error"
        };

        IReadOnlyDictionary<string, IReadOnlyList<string>>? validation = null;
        if (apiResult.ValidationErrors is not null)
        {
            validation = apiResult.ValidationErrors.ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<string>)x.Value);
        }

        return new McpToolError(
            code,
            apiResult.Message ?? "Service returned an error.",
            apiResult.StatusCode,
            validation);
    }

    public static McpToolError ToMcpError(this ApiError apiError) =>
        ((ApiResult)apiError).ToMcpError();
}
