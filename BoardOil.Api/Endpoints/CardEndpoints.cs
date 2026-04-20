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
            .AddEndpointFilter<RequireActorUserIdFilter>()
            .WithTags("Cards");

        cardEndpoints.MapPost(string.Empty, async (int boardId, CreateCardRequest request, ICardService cardService, HttpContext httpContext) =>
            (await cardService.CreateCardAsync(boardId, request, httpContext.GetActorUserId())).ToHttpResult());

        cardEndpoints.MapGet("/archived", async (int boardId, string? search, int? offset, int? limit, ICardArchiveService cardArchiveService, HttpContext httpContext) =>
            (await cardArchiveService.GetArchivedCardsAsync(boardId, search, offset, limit, httpContext.GetActorUserId())).ToHttpResult());

        cardEndpoints.MapGet("/archived/{archivedCardId:int}", async (int boardId, int archivedCardId, ICardArchiveService cardArchiveService, HttpContext httpContext) =>
            (await cardArchiveService.GetArchivedCardAsync(boardId, archivedCardId, httpContext.GetActorUserId())).ToHttpResult());

        cardEndpoints.MapPut("/{id:int}", async (int boardId, int id, UpdateCardRequest request, ICardService cardService, HttpContext httpContext) =>
            (await cardService.UpdateCardAsync(boardId, id, request, httpContext.GetActorUserId())).ToHttpResult());

        cardEndpoints.MapPatch("/{id:int}/move", async (int boardId, int id, MoveCardRequest request, ICardService cardService, HttpContext httpContext) =>
            (await cardService.MoveCardAsync(boardId, id, request, httpContext.GetActorUserId())).ToHttpResult());

        cardEndpoints.MapPost("/{id:int}/archive", async (int boardId, int id, ICardArchiveService cardArchiveService, HttpContext httpContext) =>
            (await cardArchiveService.ArchiveCardAsync(boardId, id, httpContext.GetActorUserId())).ToHttpResult());

        cardEndpoints.MapPost("/archive", async (int boardId, ArchiveCardsRequest request, ICardArchiveService cardArchiveService, HttpContext httpContext) =>
            (await cardArchiveService.ArchiveCardsAsync(boardId, request, httpContext.GetActorUserId())).ToHttpResult());

        cardEndpoints.MapDelete("/{id:int}", async (int boardId, int id, ICardService cardService, HttpContext httpContext) =>
            (await cardService.DeleteCardAsync(boardId, id, httpContext.GetActorUserId())).ToHttpResult());

        return app;
    }
}
