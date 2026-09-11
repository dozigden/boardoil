using System.Net.Http.Headers;
using BoardOil.Abstractions.Attachment;
using BoardOil.Api.Configuration;
using BoardOil.Api.Extensions;
using BoardOil.Contracts.Common;
using Microsoft.AspNetCore.Http.Features;

namespace BoardOil.Api.Endpoints;

public static class AttachmentTransferEndpoints
{
    public const string AuthorisationScheme = "BoardOilAttachment";
    public const string DownloadPath = "/api/attachment-transfers/{id:int}/download";
    public const string UploadPath = "/api/attachment-transfers/{id:int}/upload";
    public static string DownloadUrlPath(int id) => $"/api/attachment-transfers/{id}/download";
    public static string UploadUrlPath(int id) => $"/api/attachment-transfers/{id}/upload";

    public static void MapAttachmentTransferEndpoints(this IEndpointRouteBuilder app)
    {
        // This route authenticates ONLY the transfer ticket. Cookies, PATs and OAuth bearer tokens
        // cannot substitute for it. AllowAnonymous bypasses the unrelated general API identity policy.
        app.MapGet(DownloadPath, async (int id, HttpContext context, IAttachmentTransferService service, JwtAuthOptions options) =>
        {
            SetProtectedHeaders(context.Response);
            if (RequireTransfer(context, options, "download") is { } error) { return error; }
            var result = await service.DownloadAsync(id, GetSecret(context.Request), context.RequestAborted);
            if (!result.Success)
            {
                if (result.StatusCode == 401) { context.Response.Headers.WWWAuthenticate = AuthorisationScheme; }
                return result.ToHttpResult();
            }
            return Results.File(result.Data!.Content, "application/octet-stream", result.Data.FileName);
        }).AllowAnonymous().WithTags("Attachments");

        app.MapPut(UploadPath, async (int id, HttpContext context, IAttachmentTransferService service,
            JwtAuthOptions options, AttachmentStorageOptions storageOptions) =>
        {
            SetProtectedHeaders(context.Response);
            if (RequireTransfer(context, options, "upload") is { } error) { return error; }
            var bodyLimit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (bodyLimit is { IsReadOnly: false }) { bodyLimit.MaxRequestBodySize = storageOptions.MaxUploadByteLength; }
            var result = await service.UploadAsync(id, GetSecret(context.Request), context.Request.ContentType,
                context.Request.ContentLength, context.Request.Body, context.RequestAborted);
            if (!result.Success && result.StatusCode == 401)
            {
                context.Response.Headers.WWWAuthenticate = AuthorisationScheme;
            }
            return result.ToHttpResult();
        }).AllowAnonymous().WithTags("Attachments");
    }

    private static IResult? RequireTransfer(HttpContext context, JwtAuthOptions options, string operation)
    {
        if (!context.Request.IsHttps && !options.AllowInsecureCookies)
        {
            return ((ApiResult)ApiErrors.BadRequest("Attachment transfers require HTTPS.")).ToHttpResult();
        }
        var values = context.Request.Headers.Authorization;
        if (values.Count == 1 && AuthenticationHeaderValue.TryParse(values[0], out var header) &&
            string.Equals(header.Scheme, AuthorisationScheme, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(header.Parameter))
        {
            return null;
        }
        context.Response.Headers.WWWAuthenticate = AuthorisationScheme;
        return ((ApiResult)ApiErrors.Unauthorized($"An attachment {operation} ticket is required.")).ToHttpResult();
    }

    private static string GetSecret(HttpRequest request) =>
        AuthenticationHeaderValue.Parse(request.Headers.Authorization.ToString()).Parameter!;

    private static void SetProtectedHeaders(HttpResponse response)
    {
        response.Headers.CacheControl = "private, no-store";
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers.Pragma = "no-cache";
    }
}
