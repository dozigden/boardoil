using BoardOil.Api.Extensions;
using BoardOil.Api.Auth;
using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Card;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class CardEndpoints
{
    public static IEndpointRouteBuilder MapCardEndpoints(this IEndpointRouteBuilder app)
    {
        var cardEndpoints = app
            .MapGroup("/api/boards/{boardId:int}/cards")
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .AddEndpointFilter<RequireActorUserIdFilter>();

        cardEndpoints.MapPost(string.Empty, async (int boardId, CreateCardRequest request, ICardService cardService, HttpContext httpContext) =>
            (await cardService.CreateCardAsync(boardId, request, httpContext.GetActorUserId())).ToHttpResult());

        cardEndpoints.MapPut("/{id:int}", async (int boardId, int id, UpdateCardRequest request, ICardService cardService, HttpContext httpContext) =>
            (await cardService.UpdateCardAsync(boardId, id, request, httpContext.GetActorUserId())).ToHttpResult());

        cardEndpoints.MapPatch("/{id:int}/move", async (int boardId, int id, MoveCardRequest request, ICardService cardService, HttpContext httpContext) =>
            (await cardService.MoveCardAsync(boardId, id, request, httpContext.GetActorUserId())).ToHttpResult());

        cardEndpoints.MapDelete("/{id:int}", async (int boardId, int id, ICardService cardService, HttpContext httpContext) =>
            (await cardService.DeleteCardAsync(boardId, id, httpContext.GetActorUserId())).ToHttpResult());

        return app;
    }
}
