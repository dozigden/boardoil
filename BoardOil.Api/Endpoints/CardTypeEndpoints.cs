using BoardOil.Api.Auth;
using BoardOil.Api.Extensions;
using BoardOil.Abstractions.CardType;
using BoardOil.Contracts.CardType;
using BoardOil.Services.Auth;
using BoardOil.Services.CustomField;
using BoardOil.Contracts.Common;

namespace BoardOil.Api.Endpoints;

public static class CardTypeEndpoints
{
    public static IEndpointRouteBuilder MapCardTypeEndpoints(this IEndpointRouteBuilder app)
    {
        var cardTypeEndpoints = app
            .MapGroup("/api/boards/{boardId:int}/card-types")
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .AddEndpointFilter<RequireActorUserIdFilter>()
            .WithTags("Card Types");

        cardTypeEndpoints.MapGet(string.Empty, async (int boardId, ICardTypeService cardTypeService, HttpContext httpContext) =>
            (await cardTypeService.GetCardTypesAsync(boardId, httpContext.GetActorUserId())).ToHttpResult());

        cardTypeEndpoints.MapPost(string.Empty, async (int boardId, CreateCardTypeRequest request, ICardTypeService service, HttpContext context) =>
            (await service.CreateCardTypeAsync(boardId, request, context.GetActorUserId())).ToHttpResult());

        cardTypeEndpoints.MapPut("/{cardTypeId:int}", async (int boardId, int cardTypeId, UpdateCardTypeRequest request, ICardTypeService service, HttpContext context) =>
            (await service.UpdateCardTypeAsync(boardId, cardTypeId, request, context.GetActorUserId())).ToHttpResult());

        cardTypeEndpoints.MapPatch("/{cardTypeId:int}/default", async (int boardId, int cardTypeId, ICardTypeService cardTypeService, HttpContext httpContext) =>
            (await cardTypeService.SetDefaultCardTypeAsync(boardId, cardTypeId, httpContext.GetActorUserId())).ToHttpResult());

        cardTypeEndpoints.MapDelete("/{cardTypeId:int}", async (int boardId, int cardTypeId, ICardTypeService cardTypeService, HttpContext httpContext) =>
            (await cardTypeService.DeleteCardTypeAsync(boardId, cardTypeId, httpContext.GetActorUserId())).ToHttpResult());

        return app;
    }
}
