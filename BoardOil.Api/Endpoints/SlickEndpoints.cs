using BoardOil.Api.Auth;
using BoardOil.Api.Extensions;
using BoardOil.Abstractions.Slick;
using BoardOil.Contracts.Slick;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class SlickEndpoints
{
    public static IEndpointRouteBuilder MapSlickEndpoints(this IEndpointRouteBuilder app)
    {
        var slickEndpoints = app
            .MapGroup("/api/boards/{boardId:int}/slicks")
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .AddEndpointFilter<RequireActorUserIdFilter>()
            .WithTags("Slicks");

        slickEndpoints.MapGet(string.Empty, async (int boardId, ISlickService slickService, HttpContext httpContext) =>
            (await slickService.GetSlicksAsync(boardId, httpContext.GetActorUserId())).ToHttpResult());

        slickEndpoints.MapGet("/create-default-style", async (int boardId, ISlickService slickService, HttpContext httpContext) =>
            (await slickService.GetCreateDefaultStyleAsync(boardId, httpContext.GetActorUserId())).ToHttpResult());

        slickEndpoints.MapPost(string.Empty, async (int boardId, CreateSlickRequest request, ISlickService slickService, HttpContext httpContext) =>
            (await slickService.CreateSlickAsync(boardId, request, httpContext.GetActorUserId())).ToHttpResult());

        slickEndpoints.MapPut("/{slickId:int}", async (int boardId, int slickId, UpdateSlickRequest request, ISlickService slickService, HttpContext httpContext) =>
            (await slickService.UpdateSlickAsync(boardId, slickId, request, httpContext.GetActorUserId())).ToHttpResult());

        slickEndpoints.MapDelete("/{slickId:int}", async (int boardId, int slickId, ISlickService slickService, HttpContext httpContext) =>
            (await slickService.DeleteSlickAsync(boardId, slickId, httpContext.GetActorUserId())).ToHttpResult());

        return app;
    }
}
