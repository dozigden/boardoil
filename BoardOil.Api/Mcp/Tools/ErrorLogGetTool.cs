using BoardOil.Abstractions.ErrorLogs;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.ErrorLogs;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class ErrorLogGetTool(IErrorLogService errorLogs, IMcpAuthorisationService authorisationService)
    : McpToolBase<ErrorLogGetInput, ErrorLogDetailsDto>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.ErrorLogGet,
        "Get an application error by id from error_log_list, including stack trace, sanitised context, request, actor, trace identifier and linked job id where available. Requires an active administrator and explicit mcp:diagnostics:read permission.",
        ToolSchemas.ErrorLogGetInput, ToolSchemas.ErrorLogGetOutput, MachinePatScopes.McpDiagnosticsRead,
        ToolDiscoveryOrder.ErrorLogGet, McpToolBehaviours.ReadOnly, RequiresDiagnostics: true);

    protected override async Task<McpToolResult<ErrorLogDetailsDto>> ExecuteCoreAsync(
        McpInvocationContext context, ErrorLogGetInput input, CancellationToken cancellationToken)
    {
        var errors = McpToolCallHelpers.ValidateRequiredIdentifier(input.Id, "id");
        if (errors.Count > 0)
        {
            return Failure(errors);
        }
        var result = await errorLogs.GetAsync(input.Id!.Value);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }
        return Success(result.Data);
    }
}
