using BoardOil.Abstractions.Attachment;
using BoardOil.Api.Auth;
using BoardOil.Api.Extensions;
using BoardOil.Contracts.Common;
using BoardOil.Services.Auth;
using Microsoft.AspNetCore.Http.Features;

namespace BoardOil.Api.Endpoints;

public static class AttachmentEndpoints
{
    public static void MapAttachmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/boards/{boardId:int}")
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser)
            .AddEndpointFilter<RequireActorUserIdFilter>().WithTags("Attachments");
        group.MapGet("/cards/{cardId:int}/attachments", async (int boardId, int cardId, ICardAttachmentService service, HttpContext context) =>
            (await service.ListAsync(boardId, cardId, false, context.GetActorUserId())).ToHttpResult());
        group.MapGet("/cards/archived/{cardId:int}/attachments", async (int boardId, int cardId, ICardAttachmentService service, HttpContext context) =>
            (await service.ListAsync(boardId, cardId, true, context.GetActorUserId())).ToHttpResult());
        group.MapDelete("/cards/{cardId:int}/attachments/{attachmentId:int}", async (int boardId, int cardId, int attachmentId,
            ICardAttachmentService service, HttpContext context) =>
            (await service.DeleteAsync(boardId, cardId, attachmentId, context.GetActorUserId())).ToHttpResult());
        group.MapGet("/attachments/{attachmentId:int}/download", async (int boardId, int attachmentId, ICardAttachmentService service, HttpContext context) =>
        {
            var result = await service.DownloadAsync(boardId, attachmentId, context.GetActorUserId());
            if (!result.Success) { return result.ToHttpResult(); }
            context.Response.Headers.CacheControl = "private, no-store";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(result.Data!.Content, "application/octet-stream", result.Data.FileName);
        });
        group.MapPost("/cards/{cardId:int}/attachments", UploadAsync);
    }

    private static async Task<IResult> UploadAsync(int boardId, int cardId, ICardAttachmentService service,
        AttachmentStorageOptions options, HttpContext context)
    {
        var access = await service.CheckUploadAccessAsync(boardId, cardId, context.GetActorUserId());
        if (!access.Success) { return access.ToHttpResult(); }
        var request = context.Request;
        if (!request.HasFormContentType) { return InvalidFile("Upload must use multipart/form-data."); }
        var bodyLimit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodyLimit is { IsReadOnly: false }) { bodyLimit.MaxRequestBodySize = options.MaxUploadByteLength + 65536; }
        context.Features.Set<IFormFeature>(new FormFeature(request, new FormOptions
        {
            MultipartBodyLengthLimit = options.MaxUploadByteLength + 65536
        }));
        try
        {
            var form = await request.ReadFormAsync(context.RequestAborted);
            if (form.Files.Count != 1 || form.Files.GetFile("file") is not { } file)
            {
                return InvalidFile("Exactly one file is required.");
            }
            if (file.Length > options.MaxUploadByteLength)
            {
                return new ApiResult(false, 413, "Attachment exceeds the configured file size limit.").ToHttpResult();
            }
            await using var content = file.OpenReadStream();
            return (await service.UploadAsync(boardId, cardId, context.GetActorUserId(), file.FileName,
                file.ContentType, content, context.RequestAborted)).ToHttpResult();
        }
        catch (InvalidDataException) { return InvalidFile("Upload could not be read or exceeds the configured size limit."); }
    }

    private static IResult InvalidFile(string message) =>
        ((ApiResult)ApiErrors.ValidationFailed([new("file", message)])).ToHttpResult();
}
