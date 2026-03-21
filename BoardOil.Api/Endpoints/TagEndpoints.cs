using BoardOil.Api.Extensions;
using BoardOil.Abstractions.Tag;
using BoardOil.Contracts.Tag;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class TagEndpoints
{
    public static IEndpointRouteBuilder MapTagEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/tags", (ITagService tagService) =>
                tagService.GetTagsAsync().ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser);

        app.MapPost("/api/tags", (CreateTagRequest request, ITagService tagService) =>
                tagService.CreateTagAsync(request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.CardEditor);

        app.MapPatch("/api/tags/{name}", (string name, UpdateTagStyleRequest request, ITagService tagService) =>
                tagService.UpdateTagStyleAsync(name, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.CardEditor);

        return app;
    }
}
