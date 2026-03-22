using BoardOil.Api.Extensions;
using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Card;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class CardEndpoints
{
    public static IEndpointRouteBuilder MapCardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/cards", (CreateCardRequest request, ICardService cardService) =>
                cardService.CreateCardAsync(request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.CardEditor);

        app.MapPatch("/api/cards/{id:int}", (int id, UpdateCardRequest request, ICardService cardService) =>
                cardService.UpdateCardAsync(id, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.CardEditor);

        app.MapPatch("/api/cards/{id:int}/move", (int id, MoveCardRequest request, ICardService cardService) =>
                cardService.MoveCardAsync(id, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.CardEditor);

        app.MapDelete("/api/cards/{id:int}", (int id, ICardService cardService) =>
                cardService.DeleteCardAsync(id).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.CardEditor);

        return app;
    }
}
