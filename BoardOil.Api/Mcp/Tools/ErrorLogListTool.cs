using BoardOil.Abstractions.ErrorLogs;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.ErrorLogs;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class ErrorLogListTool(IErrorLogService errorLogs, IMcpAuthorisationService authorisationService)
    : McpToolBase<ErrorLogSearchRequest, ErrorLogListDto>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.ErrorLogList,
        "List application errors newest first, optionally filtering by UTC time range, exact source/area/trace identifier and a case-insensitive literal message substring. Returns paginated summaries; use error_log_get for stack trace and context. Requires an active administrator and explicit mcp:diagnostics:read permission.",
        ToolSchemas.ErrorLogListInput, ToolSchemas.ErrorLogListOutput, MachinePatScopes.McpDiagnosticsRead,
        ToolDiscoveryOrder.ErrorLogList, McpToolBehaviours.ReadOnly, RequiresDiagnostics: true);

    protected override async Task<McpToolResult<ErrorLogListDto>> ExecuteCoreAsync(
        McpInvocationContext context, ErrorLogSearchRequest input, CancellationToken cancellationToken)
    {
        var result = await errorLogs.SearchAsync(input, cancellationToken);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }
        return Success(result.Data);
    }
}
