using BoardOil.Abstractions.CustomField;
using BoardOil.Abstractions.Board;
using BoardOil.Api.Auth;
using BoardOil.Api.Extensions;
using BoardOil.Contracts.CustomField;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Endpoints;

public static class CustomFieldEndpoints
{
    public static IEndpointRouteBuilder MapCustomFieldEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/boards/{boardId:int}")
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .AddEndpointFilter<RequireActorUserIdFilter>()
            .WithTags("Custom CustomFieldValues");
        group.MapGet("/custom-fields", async (int boardId, ICustomFieldService service, HttpContext context) =>
            (await service.GetAsync(boardId, context.GetActorUserId())).ToHttpResult());
        group.MapPost("/custom-fields", async (int boardId, CustomFieldInput input, ICustomFieldService service, HttpContext context) =>
            (await service.CreateAsync(boardId, input, context.GetActorUserId())).ToHttpResult()).Accepts<CustomFieldDto>("application/json");
        group.MapPut("/custom-fields/{code}", async (int boardId, string code, CustomFieldInput input, ICustomFieldService service, HttpContext context) =>
            (await service.UpdateAsync(boardId, code, input, context.GetActorUserId())).ToHttpResult()).Accepts<CustomFieldDto>("application/json");
        group.MapDelete("/custom-fields/{code}", async (int boardId, string code, ICustomFieldService service, HttpContext context) =>
            (await service.DeleteAsync(boardId, code, context.GetActorUserId())).ToHttpResult());
        group.MapPut("/custom-field-order", async (int boardId, CustomFieldOrderRequest request, ICustomFieldService service, HttpContext context) =>
            (await service.ReorderAsync(boardId, request, context.GetActorUserId())).ToHttpResult());
        group.MapPost("/custom-fields/{code}/preview", async (int boardId, string code, CustomFieldPreviewRequest request, ICustomFieldService service, HttpContext context) =>
            (await service.PreviewAsync(boardId, code, request, context.GetActorUserId())).ToHttpResult()).Accepts<CustomFieldPreviewContract>("application/json");
        return app;
    }
}
