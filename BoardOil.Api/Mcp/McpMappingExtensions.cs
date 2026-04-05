using BoardOil.Contracts.Board;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Contracts;
using BoardOil.Mcp.Contracts;

namespace BoardOil.Api.Mcp;

public static class McpMappingExtensions
{
    public static McpBoardSnapshot ToMcp(this BoardDto board) =>
        new(
            board.Id,
            board.Name,
            board.UpdatedAtUtc,
            board.Columns
                .Select(column => new McpColumnSnapshot(
                    column.Id,
                    column.Title,
                    column.SortKey,
                    column.Cards.Select(card => card.ToMcp()).ToArray()))
                .ToArray());

    public static McpCardSnapshot ToMcp(this CardDto card) =>
        new(
            card.Id,
            card.BoardColumnId,
            card.CardTypeId,
            card.CardTypeName,
            card.CardTypeEmoji,
            card.Title,
            card.Description,
            card.SortKey,
            card.TagNames,
            card.UpdatedAtUtc);

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
