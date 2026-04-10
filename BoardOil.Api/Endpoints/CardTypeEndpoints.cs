using BoardOil.Api.Auth;
using BoardOil.Api.Extensions;
using BoardOil.Abstractions.CardType;
using BoardOil.Contracts.CardType;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class CardTypeEndpoints
{
    public static IEndpointRouteBuilder MapCardTypeEndpoints(this IEndpointRouteBuilder app)
    {
        var cardTypeEndpoints = app
            .MapGroup("/api/boards/{boardId:int}/card-types")
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .AddEndpointFilter<RequireActorUserIdFilter>();

        cardTypeEndpoints.MapGet(string.Empty, async (int boardId, ICardTypeService cardTypeService, HttpContext httpContext) =>
            (await cardTypeService.GetCardTypesAsync(boardId, httpContext.GetActorUserId())).ToHttpResult());

        cardTypeEndpoints.MapPost(string.Empty, async (int boardId, CreateCardTypeRequest request, ICardTypeService cardTypeService, HttpContext httpContext) =>
            (await cardTypeService.CreateCardTypeAsync(boardId, request, httpContext.GetActorUserId())).ToHttpResult());

        cardTypeEndpoints.MapPut("/{cardTypeId:int}", async (int boardId, int cardTypeId, UpdateCardTypeRequest request, ICardTypeService cardTypeService, HttpContext httpContext) =>
            (await cardTypeService.UpdateCardTypeAsync(boardId, cardTypeId, request, httpContext.GetActorUserId())).ToHttpResult());

        cardTypeEndpoints.MapPatch("/{cardTypeId:int}/default", async (int boardId, int cardTypeId, ICardTypeService cardTypeService, HttpContext httpContext) =>
            (await cardTypeService.SetDefaultCardTypeAsync(boardId, cardTypeId, httpContext.GetActorUserId())).ToHttpResult());

        cardTypeEndpoints.MapDelete("/{cardTypeId:int}", async (int boardId, int cardTypeId, ICardTypeService cardTypeService, HttpContext httpContext) =>
            (await cardTypeService.DeleteCardTypeAsync(boardId, cardTypeId, httpContext.GetActorUserId())).ToHttpResult());

        return app;
    }
}
