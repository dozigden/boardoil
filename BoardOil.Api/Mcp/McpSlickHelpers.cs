using BoardOil.Abstractions.Slick;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;

namespace BoardOil.Api.Mcp;

internal static class McpSlickHelpers
{
    public static async Task<(bool Success, IReadOnlyDictionary<int, McpCardSlickSnapshot>? SlicksById, ApiResult? Error)> LoadBoardSlicksByIdAsync(
        ISlickService slickService,
        int boardId,
        int actorUserId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = await slickService.GetSlicksAsync(boardId, actorUserId);
        if (!result.Success || result.Data is null)
        {
            return (false, null, result);
        }

        var slicksById = result.Data
            .Select(x => x.ToMcp())
            .ToDictionary(x => x.Id, x => x);
        return (true, slicksById, null);
    }
}
