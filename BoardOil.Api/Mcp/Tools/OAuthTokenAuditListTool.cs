using BoardOil.Abstractions.OAuth;
using BoardOil.Contracts.Auth;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class OAuthTokenAuditListTool(IOAuthTokenAuditService audits, IMcpAuthorisationService authorisationService)
    : McpToolBase<OAuthTokenAuditListInput, OAuthTokenAuditListOutput>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.OAuthTokenAuditList,
        "List recorded OAuth token-exchange audit events newest first, with pagination and optional time, outcome, grant, connection, client, authorization and token-fingerprint filters. Returns only diagnostic identifiers, scopes, outcomes, error codes and SHA-256 token fingerprints, without tokens or free-text error details. Reads existing records without enabling capture; an empty result does not prove no exchanges occurred. Requires an active administrator and explicit mcp:diagnostics:read permission.",
        ToolSchemas.OAuthTokenAuditListInput, ToolSchemas.OAuthTokenAuditListOutput, MachinePatScopes.McpDiagnosticsRead,
        ToolDiscoveryOrder.OAuthTokenAuditList, McpToolBehaviours.ReadOnly, RequiresDiagnostics: true);

    protected override async Task<McpToolResult<OAuthTokenAuditListOutput>> ExecuteCoreAsync(
        McpInvocationContext context, OAuthTokenAuditListInput input, CancellationToken cancellationToken)
    {
        var result = await audits.ListAsync(
            input.Offset, input.Limit, input.FromUtc?.UtcDateTime, input.ToUtc?.UtcDateTime,
            input.Outcome, input.GrantType, input.ConnectionId, input.ClientId,
            input.AuthorizationId, input.TokenFingerprint);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        var page = result.Data;
        return Success(new OAuthTokenAuditListOutput(
            page.Items.Select(audit => new McpOAuthTokenAudit(
                audit.Id, audit.OccurredAtUtc, audit.Outcome, audit.GrantType,
                audit.RequestedScopes, audit.ErrorCode, audit.PresentedTokenFingerprint,
                audit.IssuedRefreshTokenFingerprint, audit.AuthorizationId,
                audit.OAuthClientId, audit.OAuthConnectionId, audit.TraceIdentifier)).ToArray(),
            page.Offset, page.Limit, page.TotalCount));
    }
}
