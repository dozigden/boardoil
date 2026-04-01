using BoardOil.Api.Extensions;
using BoardOil.Abstractions.Tag;
using BoardOil.Contracts.Tag;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class TagEndpoints
{
    public static IEndpointRouteBuilder MapTagEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/boards/{boardId:int}/tags", (int boardId, ITagService tagService) =>
                tagService.GetTagsAsync(boardId).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser);

        app.MapPost("/api/boards/{boardId:int}/tags", (int boardId, CreateTagRequest request, ITagService tagService) =>
                tagService.CreateTagAsync(boardId, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.CardEditor);

        app.MapPut("/api/boards/{boardId:int}/tags/{tagId:int}", (int boardId, int tagId, UpdateTagStyleRequest request, ITagService tagService) =>
                tagService.UpdateTagStyleAsync(boardId, tagId, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.CardEditor);

        app.MapDelete("/api/boards/{boardId:int}/tags/{tagId:int}", (int boardId, int tagId, ITagService tagService) =>
                tagService.DeleteTagAsync(boardId, tagId).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.CardEditor);

        return app;
    }
}
