using BoardOil.Api.Extensions;
using BoardOil.Abstractions.Column;
using BoardOil.Contracts.Column;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class ColumnEndpoints
{
    public static IEndpointRouteBuilder MapColumnEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/boards/{boardId:int}/columns", (int boardId, IColumnService columnService) =>
                columnService.GetColumnsAsync(boardId).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);

        app.MapPost("/api/boards/{boardId:int}/columns", (int boardId, CreateColumnRequest request, IColumnService columnService) =>
                columnService.CreateColumnAsync(boardId, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);

        app.MapPut("/api/boards/{boardId:int}/columns/{id:int}", (int boardId, int id, UpdateColumnRequest request, IColumnService columnService) =>
                columnService.UpdateColumnAsync(boardId, id, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);

        app.MapPatch("/api/boards/{boardId:int}/columns/{id:int}/move", (int boardId, int id, MoveColumnRequest request, IColumnService columnService) =>
                columnService.MoveColumnAsync(boardId, id, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);

        app.MapDelete("/api/boards/{boardId:int}/columns/{id:int}", (int boardId, int id, IColumnService columnService) =>
                columnService.DeleteColumnAsync(boardId, id).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);

        return app;
    }
}
