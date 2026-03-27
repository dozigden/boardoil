using BoardOil.Api.Extensions;
using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Card;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class CardEndpoints
{
    public static IEndpointRouteBuilder MapCardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/boards/{boardId:int}/cards", (int boardId, CreateCardRequest request, ICardService cardService) =>
                cardService.CreateCardAsync(boardId, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.CardEditor);

        app.MapPut("/api/boards/{boardId:int}/cards/{id:int}", (int boardId, int id, UpdateCardRequest request, ICardService cardService) =>
                cardService.UpdateCardAsync(boardId, id, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.CardEditor);

        app.MapPatch("/api/boards/{boardId:int}/cards/{id:int}/move", (int boardId, int id, MoveCardRequest request, ICardService cardService) =>
                cardService.MoveCardAsync(boardId, id, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.CardEditor);

        app.MapDelete("/api/boards/{boardId:int}/cards/{id:int}", (int boardId, int id, ICardService cardService) =>
                cardService.DeleteCardAsync(boardId, id).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.CardEditor);

        return app;
    }
}
