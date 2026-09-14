using BoardOil.Abstractions.Attachment;
using BoardOil.Api.Auth;
using BoardOil.Api.Extensions;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.Card;
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
        group.MapGet("/attachments", async (int boardId, int? offset, int? limit, string? sort, string? direction,
            string? state, IBoardAttachmentInventoryService service, HttpContext context) =>
            (await service.ListAsync(boardId, context.GetActorUserId(),
                new BoardAttachmentInventoryQuery(offset ?? 0, limit ?? 50, sort ?? "date", direction ?? "desc", state ?? "both"),
                context.RequestAborted)).ToHttpResult());
        group.MapGet("/cards/{cardId:int}/attachments", async (int boardId, int cardId, ICardAttachmentService service, HttpContext context) =>
            (await service.ListAsync(boardId, cardId, false, context.GetActorUserId())).ToHttpResult());
        group.MapGet("/cards/archived/{cardId:int}/attachments", async (int boardId, int cardId, ICardAttachmentService service, HttpContext context) =>
            (await service.ListAsync(boardId, cardId, true, context.GetActorUserId())).ToHttpResult());
        group.MapGet("/attachment-images/first-by-card", async (
            int boardId,
            int[]? cardId,
            IBoardAttachmentImageQueryService service,
            HttpContext context) =>
            (await service.ListFirstByCardAsync(
                boardId,
                cardId,
                context.GetActorUserId(),
                context.RequestAborted)).ToHttpResult());
        group.MapGet("/cards/{cardId:int}/attachments/image-content", (int boardId, int cardId, string fileName,
            ICardAttachmentService service, HttpContext context) =>
            ViewImageAsync(boardId, cardId, false, fileName, service, context));
        group.MapGet("/cards/archived/{cardId:int}/attachments/image-content", (int boardId, int cardId, string fileName,
            ICardAttachmentService service, HttpContext context) =>
            ViewImageAsync(boardId, cardId, true, fileName, service, context));
        group.MapDelete("/cards/{cardId:int}/attachments/{attachmentId:int}", async (int boardId, int cardId, int attachmentId,
            ICardAttachmentService service, HttpContext context) =>
            (await service.DeleteAsync(boardId, cardId, attachmentId, context.GetActorUserId())).ToHttpResult());
        group.MapGet("/attachments/{attachmentId:int}/thumbnail", ViewThumbnailAsync);
        group.MapPut("/attachments/{attachmentId:int}/thumbnail", PutThumbnailAsync);
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

    private static async Task<IResult> ViewThumbnailAsync(int boardId, int attachmentId,
        ICardAttachmentService service, HttpContext context)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        var result = await service.ViewThumbnailAsync(boardId, attachmentId, context.GetActorUserId());
        if (!result.Success) { return result.ToHttpResult(); }
        if (result.Data!.Content.CanSeek) { context.Response.ContentLength = result.Data.Content.Length; }
        return Results.Stream(result.Data.Content, "image/png");
    }

    private static async Task<IResult> PutThumbnailAsync(int boardId, int attachmentId,
        ICardAttachmentService service, AttachmentStorageOptions options, HttpContext context)
    {
        var bodyLimit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodyLimit is { IsReadOnly: false }) { bodyLimit.MaxRequestBodySize = options.MaxThumbnailByteLength; }
        if (context.Request.ContentLength > options.MaxThumbnailByteLength)
        {
            return new ApiResult(false, 413, $"Thumbnail must be {options.MaxThumbnailByteLength} bytes or smaller.").ToHttpResult();
        }
        return (await service.PutThumbnailAsync(boardId, attachmentId, context.GetActorUserId(),
            context.Request.ContentType, context.Request.Body, context.RequestAborted)).ToHttpResult();
    }

    private static async Task<IResult> ViewImageAsync(int boardId, int cardId, bool archived, string fileName,
        ICardAttachmentService service, HttpContext context)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        var result = await service.ViewImageAsync(boardId, cardId, archived, fileName, context.GetActorUserId(), context.RequestAborted);
        if (!result.Success) { return result.ToHttpResult(); }
        if (result.Data!.Content.CanSeek) { context.Response.ContentLength = result.Data.Content.Length; }
        return Results.Stream(result.Data.Content, result.Data.ContentType);
    }

    private static async Task<IResult> UploadAsync(int boardId, int cardId, ICardAttachmentService service,
        AttachmentStorageOptions options, HttpContext context)
    {
        var access = await service.CheckUploadAccessAsync(boardId, cardId, context.GetActorUserId());
        if (!access.Success) { return access.ToHttpResult(); }
        var request = context.Request;
        if (!request.HasFormContentType) { return InvalidFile("Upload must use multipart/form-data."); }
        var bodyLimit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        var multipartLimit = options.MaxUploadByteLength + options.MaxThumbnailByteLength + 65536;
        if (bodyLimit is { IsReadOnly: false }) { bodyLimit.MaxRequestBodySize = multipartLimit; }
        context.Features.Set<IFormFeature>(new FormFeature(request, new FormOptions
        {
            MultipartBodyLengthLimit = multipartLimit
        }));
        try
        {
            var form = await request.ReadFormAsync(context.RequestAborted);
            var file = form.Files.GetFile("file");
            var thumbnail = form.Files.GetFile("thumbnail");
            var expectedFileCount = thumbnail is null ? 1 : 2;
            if (file is null || form.Files.Count != expectedFileCount)
            {
                return InvalidFile("Exactly one file and an optional thumbnail are required.");
            }
            if (file.Length > options.MaxUploadByteLength)
            {
                return new ApiResult(false, 413, "Attachment exceeds the configured file size limit.").ToHttpResult();
            }
            await using var content = file.OpenReadStream();
            await using var thumbnailContent = thumbnail?.Length <= options.MaxThumbnailByteLength
                ? thumbnail.OpenReadStream()
                : null;
            return (await service.UploadAsync(boardId, cardId, context.GetActorUserId(), file.FileName,
                file.ContentType, content, thumbnailContent, thumbnail?.ContentType, context.RequestAborted)).ToHttpResult();
        }
        catch (InvalidDataException) { return InvalidFile("Upload could not be read or exceeds the configured size limit."); }
    }

    private static IResult InvalidFile(string message) =>
        ((ApiResult)ApiErrors.ValidationFailed([new("file", message)])).ToHttpResult();
}
