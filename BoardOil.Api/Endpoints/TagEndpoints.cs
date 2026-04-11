using BoardOil.Api.Extensions;
using BoardOil.Api.Auth;
using BoardOil.Abstractions.Tag;
using BoardOil.Contracts.Tag;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class TagEndpoints
{
    public static IEndpointRouteBuilder MapTagEndpoints(this IEndpointRouteBuilder app)
    {
        var tagEndpoints = app
            .MapGroup("/api/boards/{boardId:int}/tags")
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .AddEndpointFilter<RequireActorUserIdFilter>()
            .WithTags("Tags");

        tagEndpoints.MapGet(string.Empty, async (int boardId, ITagService tagService, HttpContext httpContext) =>
            (await tagService.GetTagsAsync(boardId, httpContext.GetActorUserId())).ToHttpResult());

        tagEndpoints.MapPost(string.Empty, async (int boardId, CreateTagRequest request, ITagService tagService, HttpContext httpContext) =>
            (await tagService.CreateTagAsync(boardId, request, httpContext.GetActorUserId())).ToHttpResult());

        tagEndpoints.MapPut("/{tagId:int}", async (int boardId, int tagId, UpdateTagStyleRequest request, ITagService tagService, HttpContext httpContext) =>
            (await tagService.UpdateTagStyleAsync(boardId, tagId, request, httpContext.GetActorUserId())).ToHttpResult());

        tagEndpoints.MapDelete("/{tagId:int}", async (int boardId, int tagId, ITagService tagService, HttpContext httpContext) =>
            (await tagService.DeleteTagAsync(boardId, tagId, httpContext.GetActorUserId())).ToHttpResult());

        return app;
    }
}
