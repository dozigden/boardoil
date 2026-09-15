using BoardOil.Abstractions.Attachment;
using BoardOil.Api.Configuration;
using BoardOil.Api.Endpoints;
using BoardOil.Api.OAuth;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardAttachmentUploadTool(IAttachmentTransferService transfers, IHttpContextAccessor httpContextAccessor,
    OAuthEndpointUrlResolver urls, JwtAuthOptions options, BoardOilMcpOptions mcpOptions,
    IMcpAuthorisationService authorisationService)
    : McpToolBase<CardAttachmentUploadInput, CardAttachmentUploadOutput>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(ToolNames.CardAttachmentUpload,
        "Issue a one-attempt HTTP upload ticket for a saved live card. Send the exact raw file bytes using the returned URL, PUT method and headers. After a successful PUT, markdownSnippet can be inserted into the card description with card_update. Failed or interrupted uploads require a new ticket. Requires authenticated MCP.",
        ToolSchemas.CardAttachmentUploadInput, ToolSchemas.CardAttachmentUploadOutput, MachinePatScopes.McpWrite,
        ToolDiscoveryOrder.CardAttachmentUpload);

    protected override async Task<McpToolResult<CardAttachmentUploadOutput>> ExecuteCoreAsync(
        McpInvocationContext context, CardAttachmentUploadInput input, CancellationToken cancellationToken)
    {
        IReadOnlyList<ValidationError> errors =
        [
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId"),
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.CardId, "cardId")
        ];
        if (string.IsNullOrWhiteSpace(input.FileName)) { errors = [..errors, new("fileName", "'fileName' is required.")]; }
        if (input.ByteLength is null or < 0) { errors = [..errors, new("byteLength", "'byteLength' is required and must be zero or greater.")]; }
        if (errors.Count > 0) { return Failure(errors); }
        if (mcpOptions.AuthMode == McpAuthMode.None || context.AccessContext?.Credential is not { } credential)
        {
            return Failure(new McpToolError("unauthorised", "Upload tickets require authenticated MCP.", 401));
        }
        var accessError = AuthorisationService.EnsureToolAccess(context.AccessContext, Definition.RequiredScope, input.BoardId!.Value);
        if (accessError is not null) { return Failure(accessError); }
        var http = httpContextAccessor.HttpContext;
        if (http is null || (!http.Request.IsHttps && !options.AllowInsecureCookies))
        {
            return Failure(new McpToolError("invalid_request", "Attachment transfers require HTTPS.", 400));
        }
        var baseUrl = await urls.GetPublicBaseUrlAsync(http.Request);
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && !(options.AllowInsecureCookies && uri.Scheme == Uri.UriSchemeHttp)))
        {
            return Failure(new McpToolError("invalid_request", "The public attachment transfer URL must use HTTPS.", 400));
        }
        var result = await transfers.IssueUploadAsync(input.BoardId.Value, input.CardId!.Value, context.ActorUserId,
            input.FileName, input.ContentType, input.ByteLength!.Value, credential, cancellationToken);
        if (!result.Success || result.Data is null) { return Failure(result.ToMcpError()); }
        var markdownSnippet = $"![Image](boardoil-attachment:{Uri.EscapeDataString(result.Data.OriginalFileName)})";
        return Success(new(baseUrl + AttachmentTransferEndpoints.UploadUrlPath(result.Data.Id), "PUT",
            new Dictionary<string, string>
            {
                ["Authorization"] = $"{AttachmentTransferEndpoints.AuthorisationScheme} {result.Data.Secret}",
                ["Content-Type"] = result.Data.ContentType
            }, result.Data.ByteLength, markdownSnippet, result.Data.ExpiresAtUtc));
    }
}
