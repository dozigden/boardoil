using BoardOil.Api.Extensions;
using BoardOil.Api.Auth;
using BoardOil.Abstractions.Column;
using BoardOil.Contracts.Column;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class ColumnEndpoints
{
    public static IEndpointRouteBuilder MapColumnEndpoints(this IEndpointRouteBuilder app)
    {
        var columnEndpoints = app
            .MapGroup("/api/boards/{boardId:int}/columns")
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .AddEndpointFilter<RequireActorUserIdFilter>();

        columnEndpoints.MapGet(string.Empty, async (int boardId, IColumnService columnService, HttpContext httpContext) =>
            (await columnService.GetColumnsAsync(boardId, httpContext.GetActorUserId())).ToHttpResult());

        columnEndpoints.MapPost(string.Empty, async (int boardId, CreateColumnRequest request, IColumnService columnService, HttpContext httpContext) =>
            (await columnService.CreateColumnAsync(boardId, request, httpContext.GetActorUserId())).ToHttpResult());

        columnEndpoints.MapPut("/{id:int}", async (int boardId, int id, UpdateColumnRequest request, IColumnService columnService, HttpContext httpContext) =>
            (await columnService.UpdateColumnAsync(boardId, id, request, httpContext.GetActorUserId())).ToHttpResult());

        columnEndpoints.MapPatch("/{id:int}/move", async (int boardId, int id, MoveColumnRequest request, IColumnService columnService, HttpContext httpContext) =>
            (await columnService.MoveColumnAsync(boardId, id, request, httpContext.GetActorUserId())).ToHttpResult());

        columnEndpoints.MapDelete("/{id:int}", async (int boardId, int id, IColumnService columnService, HttpContext httpContext) =>
            (await columnService.DeleteColumnAsync(boardId, id, httpContext.GetActorUserId())).ToHttpResult());

        return app;
    }
}
