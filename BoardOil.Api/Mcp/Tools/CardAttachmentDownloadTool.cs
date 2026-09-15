using BoardOil.Abstractions.Attachment;
using BoardOil.Api.Configuration;
using BoardOil.Api.Endpoints;
using BoardOil.Api.OAuth;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardAttachmentDownloadTool(IAttachmentTransferService transfers, IHttpContextAccessor httpContextAccessor,
    OAuthEndpointUrlResolver urls, JwtAuthOptions options, BoardOilMcpOptions mcpOptions, IMcpAuthorisationService authorisationService)
    : McpToolBase<CardAttachmentDownloadInput, CardAttachmentDownloadOutput>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(ToolNames.CardAttachmentDownload,
        "Issue a short-lived HTTP download ticket for a live or archived attachment. Use the returned URL, method and Authorization header with an HTTP client to retrieve the original bytes. Treat the header as a secret; never put it in a URL. Requires authenticated MCP.",
        ToolSchemas.CardAttachmentDownloadInput, ToolSchemas.CardAttachmentDownloadOutput, MachinePatScopes.McpRead,
        ToolDiscoveryOrder.CardAttachmentDownload);

    protected override async Task<McpToolResult<CardAttachmentDownloadOutput>> ExecuteCoreAsync(
        McpInvocationContext context, CardAttachmentDownloadInput input, CancellationToken cancellationToken)
    {
        IReadOnlyList<ValidationError> errors =
        [
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId"),
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.Id, "id")
        ];
        if (errors.Count > 0) { return Failure(errors); }
        if (mcpOptions.AuthMode == McpAuthMode.None || context.AccessContext?.Credential is not { } credential)
        {
            return Failure(new McpToolError("unauthorised", "Download tickets require authenticated MCP.", 401));
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
        var result = await transfers.IssueDownloadAsync(input.BoardId.Value, input.Id!.Value, context.ActorUserId, credential, cancellationToken);
        if (!result.Success || result.Data is null) { return Failure(result.ToMcpError()); }
        return Success(new(baseUrl + AttachmentTransferEndpoints.DownloadUrlPath(result.Data.Id), "GET",
            new Dictionary<string, string>
            {
                ["Authorization"] = $"{AttachmentTransferEndpoints.AuthorisationScheme} {result.Data.Secret}"
            }, result.Data.ExpiresAtUtc));
    }
}
