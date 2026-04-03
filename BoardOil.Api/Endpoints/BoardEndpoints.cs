using BoardOil.Api.Extensions;
using BoardOil.Api.Auth;
using BoardOil.Abstractions.Board;
using BoardOil.Contracts.Board;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class BoardEndpoints
{
    public static IEndpointRouteBuilder MapBoardEndpoints(this IEndpointRouteBuilder app)
    {
        var boardEndpoints = app
            .MapGroup("/api/boards")
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .AddEndpointFilter<RequireActorUserIdFilter>();

        boardEndpoints.MapGet(string.Empty, async (IBoardService boardService, HttpContext httpContext) =>
            (await boardService.GetBoardsAsync(httpContext.GetActorUserId())).ToHttpResult());

        boardEndpoints.MapGet("/{boardId:int}", async (int boardId, IBoardService boardService, HttpContext httpContext) =>
            (await boardService.GetBoardAsync(boardId, httpContext.GetActorUserId())).ToHttpResult());

        boardEndpoints.MapPost(string.Empty, async (CreateBoardRequest request, IBoardService boardService, HttpContext httpContext) =>
            (await boardService.CreateBoardAsync(request, httpContext.GetActorUserId())).ToHttpResult());

        boardEndpoints.MapPut("/{boardId:int}", async (int boardId, UpdateBoardRequest request, IBoardService boardService, HttpContext httpContext) =>
            (await boardService.UpdateBoardAsync(boardId, request, httpContext.GetActorUserId())).ToHttpResult());

        boardEndpoints.MapDelete("/{boardId:int}", async (int boardId, IBoardService boardService, HttpContext httpContext) =>
            (await boardService.DeleteBoardAsync(boardId, httpContext.GetActorUserId())).ToHttpResult());

        boardEndpoints.MapGet("/{boardId:int}/members", async (int boardId, IBoardMemberService boardMemberService, HttpContext httpContext) =>
            (await boardMemberService.GetMembersAsync(boardId, httpContext.GetActorUserId())).ToHttpResult());

        boardEndpoints.MapPost("/{boardId:int}/members", async (int boardId, AddBoardMemberRequest request, IBoardMemberService boardMemberService, HttpContext httpContext) =>
            (await boardMemberService.AddMemberAsync(boardId, request, httpContext.GetActorUserId())).ToHttpResult());

        boardEndpoints.MapPatch("/{boardId:int}/members/{userId:int}", async (int boardId, int userId, UpdateBoardMemberRoleRequest request, IBoardMemberService boardMemberService, HttpContext httpContext) =>
            (await boardMemberService.UpdateMemberRoleAsync(boardId, userId, request, httpContext.GetActorUserId())).ToHttpResult());

        boardEndpoints.MapDelete("/{boardId:int}/members/{userId:int}", async (int boardId, int userId, IBoardMemberService boardMemberService, HttpContext httpContext) =>
            (await boardMemberService.RemoveMemberAsync(boardId, userId, httpContext.GetActorUserId())).ToHttpResult());

        return app;
    }
}
