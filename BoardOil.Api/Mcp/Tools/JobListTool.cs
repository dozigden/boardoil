using BoardOil.Abstractions.Jobs;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Jobs;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class JobListTool(IJobService jobs, IMcpAuthorisationService authorisationService)
    : McpToolBase<JobListRequest, JobListDto>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.JobList,
        "List system-wide background jobs newest first by job ID, with offset/limit pagination (default 100, maximum 200). Includes status, timing, actor, correlation ID, payload, result and failure message; use job_get for lifecycle logs. Requires an active administrator and explicit mcp:diagnostics:read permission.",
        ToolSchemas.JobListInput, ToolSchemas.JobListOutput, MachinePatScopes.McpDiagnosticsRead,
        ToolDiscoveryOrder.JobList, McpToolBehaviours.ReadOnly, RequiresDiagnostics: true);

    protected override async Task<McpToolResult<JobListDto>> ExecuteCoreAsync(
        McpInvocationContext context, JobListRequest input, CancellationToken cancellationToken)
    {
        var result = await jobs.ListAsync(input, cancellationToken);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }
        return Success(result.Data);
    }
}
