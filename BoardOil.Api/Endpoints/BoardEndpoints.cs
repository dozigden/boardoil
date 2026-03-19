using BoardOil.Api.Extensions;
using BoardOil.Abstractions.Board;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class BoardEndpoints
{
    public static IEndpointRouteBuilder MapBoardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/board", (IBoardService boardService) =>
                boardService.GetBoardAsync().ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser);

        return app;
    }
}
