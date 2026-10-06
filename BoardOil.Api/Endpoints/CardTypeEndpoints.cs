using BoardOil.Api.Auth;
using BoardOil.Api.Extensions;
using BoardOil.Abstractions.CardType;
using BoardOil.Contracts.CardType;
using BoardOil.Services.Auth;
using BoardOil.Services.CardField;
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

        cardTypeEndpoints.MapPost(string.Empty, async (int boardId, CardTypeInput input, ICardTypeService service, HttpContext context) =>
        {
            var fields = CardFieldDefinitionInputs.Parse(input.Fields);
            if (!fields.IsValid) { return ApiErrors.ValidationFailed(fields.Errors).ToHttpResult(); }
            var request = new CreateCardTypeRequest(input.Name, input.Emoji, input.StyleName, input.StylePropertiesJson, fields.Value);
            return (await service.CreateCardTypeAsync(boardId, request, context.GetActorUserId())).ToHttpResult();
        }).Accepts<CreateCardTypeRequest>("application/json");

        cardTypeEndpoints.MapPut("/{cardTypeId:int}", async (int boardId, int cardTypeId, CardTypeInput input, ICardTypeService service, HttpContext context) =>
        {
            var fields = CardFieldDefinitionInputs.Parse(input.Fields);
            if (!fields.IsValid) { return ApiErrors.ValidationFailed(fields.Errors).ToHttpResult(); }
            var request = new UpdateCardTypeRequest(input.Name, input.Emoji, input.StyleName, input.StylePropertiesJson, fields.Value);
            return (await service.UpdateCardTypeAsync(boardId, cardTypeId, request, context.GetActorUserId())).ToHttpResult();
        }).Accepts<UpdateCardTypeRequest>("application/json");

        cardTypeEndpoints.MapPost("/{cardTypeId:int}/fields/preview", async (int boardId, int cardTypeId, PreviewCardTypeFieldsInput input, ICardTypeService service, HttpContext context) =>
        {
            var fields = CardFieldDefinitionInputs.Parse(input.Fields);
            if (!fields.IsValid) { return ApiErrors.ValidationFailed(fields.Errors).ToHttpResult(); }
            return (await service.PreviewFieldsAsync(boardId, cardTypeId, new(fields.Value), context.GetActorUserId())).ToHttpResult();
        }).Accepts<PreviewCardTypeFieldsRequest>("application/json");

        cardTypeEndpoints.MapPatch("/{cardTypeId:int}/default", async (int boardId, int cardTypeId, ICardTypeService cardTypeService, HttpContext httpContext) =>
            (await cardTypeService.SetDefaultCardTypeAsync(boardId, cardTypeId, httpContext.GetActorUserId())).ToHttpResult());

        cardTypeEndpoints.MapDelete("/{cardTypeId:int}", async (int boardId, int cardTypeId, ICardTypeService cardTypeService, HttpContext httpContext) =>
            (await cardTypeService.DeleteCardTypeAsync(boardId, cardTypeId, httpContext.GetActorUserId())).ToHttpResult());

        return app;
    }
}
