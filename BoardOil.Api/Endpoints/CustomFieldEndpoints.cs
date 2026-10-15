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
            .WithTags("Custom fields");
        group.MapGet("/custom-fields", async (int boardId, ICustomFieldService service, HttpContext context) =>
            (await service.GetAsync(boardId, context.GetActorUserId())).ToHttpResult());
        group.MapPost("/custom-fields", async (int boardId, CustomFieldRequest input, ICustomFieldService service, HttpContext context) =>
            (await service.CreateAsync(boardId, input, context.GetActorUserId())).ToHttpResult());
        group.MapPut("/custom-fields/{customFieldId:int}", async (int boardId, int customFieldId, CustomFieldRequest input, ICustomFieldService service, HttpContext context) =>
            (await service.UpdateAsync(boardId, customFieldId, input, context.GetActorUserId())).ToHttpResult());
        group.MapDelete("/custom-fields/{customFieldId:int}", async (int boardId, int customFieldId, ICustomFieldService service, HttpContext context) =>
            (await service.DeleteAsync(boardId, customFieldId, context.GetActorUserId())).ToHttpResult());
        group.MapPut("/custom-field-order", async (int boardId, CustomFieldOrderRequest request, ICustomFieldService service, HttpContext context) =>
            (await service.ReorderAsync(boardId, request, context.GetActorUserId())).ToHttpResult());
        group.MapPost("/custom-fields/{customFieldId:int}/preview", async (int boardId, int customFieldId, CustomFieldPreviewRequest request, ICustomFieldService service, HttpContext context) =>
            (await service.PreviewAsync(boardId, customFieldId, request, context.GetActorUserId())).ToHttpResult());
        return app;
    }
}
