using BoardOil.Api.Auth;
using BoardOil.Api.Extensions;
using BoardOil.Abstractions.Board;
using BoardOil.Contracts.Board;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class SystemBoardEndpoints
{
    public static IEndpointRouteBuilder MapSystemBoardEndpoints(this IEndpointRouteBuilder app)
    {
        var systemBoardEndpoints = app
            .MapGroup("/api/system/boards")
            .RequireAuthorization(BoardOilPolicies.AdminOnly)
            .AddEndpointFilter<RequireActorUserIdFilter>()
            .WithTags("System Boards");

        systemBoardEndpoints.MapGet(string.Empty, async (ISystemBoardService systemBoardService) =>
            (await systemBoardService.GetBoardsAsync()).ToHttpResult());

        systemBoardEndpoints.MapGet("/{boardId:int}/members", async (int boardId, ISystemBoardService systemBoardService) =>
            (await systemBoardService.GetMembersAsync(boardId)).ToHttpResult());

        systemBoardEndpoints.MapPost("/{boardId:int}/members", async (int boardId, AddBoardMemberRequest request, ISystemBoardService systemBoardService) =>
            (await systemBoardService.AddMemberAsync(boardId, request)).ToHttpResult());

        systemBoardEndpoints.MapPatch("/{boardId:int}/members/{userId:int}", async (int boardId, int userId, UpdateBoardMemberRoleRequest request, ISystemBoardService systemBoardService) =>
            (await systemBoardService.UpdateMemberRoleAsync(boardId, userId, request)).ToHttpResult());

        systemBoardEndpoints.MapDelete("/{boardId:int}/members/{userId:int}", async (int boardId, int userId, ISystemBoardService systemBoardService) =>
            (await systemBoardService.RemoveMemberAsync(boardId, userId)).ToHttpResult());

        return app;
    }
}
