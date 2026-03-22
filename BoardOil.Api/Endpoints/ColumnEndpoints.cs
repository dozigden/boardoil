using BoardOil.Api.Extensions;
using BoardOil.Abstractions.Column;
using BoardOil.Contracts.Column;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class ColumnEndpoints
{
    public static IEndpointRouteBuilder MapColumnEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/columns", (IColumnService columnService) =>
                columnService.GetColumnsAsync().ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);

        app.MapPost("/api/columns", (CreateColumnRequest request, IColumnService columnService) =>
                columnService.CreateColumnAsync(request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);

        app.MapPut("/api/columns/{id:int}", (int id, UpdateColumnRequest request, IColumnService columnService) =>
                columnService.UpdateColumnAsync(id, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);

        app.MapPatch("/api/columns/{id:int}/move", (int id, MoveColumnRequest request, IColumnService columnService) =>
                columnService.MoveColumnAsync(id, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);

        app.MapDelete("/api/columns/{id:int}", (int id, IColumnService columnService) =>
                columnService.DeleteColumnAsync(id).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);

        return app;
    }
}
