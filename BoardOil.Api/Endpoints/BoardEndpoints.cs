using BoardOil.Api.Extensions;
using BoardOil.Abstractions.Board;
using BoardOil.Contracts.Board;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class BoardEndpoints
{
    public static IEndpointRouteBuilder MapBoardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/boards", (IBoardService boardService) =>
                boardService.GetBoardsAsync().ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser);

        app.MapGet("/api/boards/{boardId:int}", (int boardId, IBoardService boardService) =>
                boardService.GetBoardAsync(boardId).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser);

        app.MapPost("/api/boards", (CreateBoardRequest request, IBoardService boardService) =>
                boardService.CreateBoardAsync(request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);

        app.MapPut("/api/boards/{boardId:int}", (int boardId, UpdateBoardRequest request, IBoardService boardService) =>
                boardService.UpdateBoardAsync(boardId, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);

        app.MapDelete("/api/boards/{boardId:int}", (int boardId, IBoardService boardService) =>
                boardService.DeleteBoardAsync(boardId).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);

        return app;
    }
}
