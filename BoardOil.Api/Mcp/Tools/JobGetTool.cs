using BoardOil.Abstractions.Jobs;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Jobs;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class JobGetTool(IJobService jobs, IMcpAuthorisationService authorisationService)
    : McpToolBase<JobGetInput, JobDetailsDto>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.JobGet,
        "Get a background job by id from job_list or an error log linked jobId. Includes status, timing, actor, correlation ID, payload, result, failure message and all stored lifecycle logs in chronological order. Requires an active administrator and explicit mcp:diagnostics:read permission.",
        ToolSchemas.JobGetInput, ToolSchemas.JobGetOutput, MachinePatScopes.McpDiagnosticsRead,
        ToolDiscoveryOrder.JobGet, McpToolBehaviours.ReadOnly, RequiresDiagnostics: true);

    protected override async Task<McpToolResult<JobDetailsDto>> ExecuteCoreAsync(
        McpInvocationContext context, JobGetInput input, CancellationToken cancellationToken)
    {
        var errors = McpToolCallHelpers.ValidateRequiredIdentifier(input.Id, "id");
        if (errors.Count > 0)
        {
            return Failure(errors);
        }
        var result = await jobs.GetAsync(input.Id!.Value, cancellationToken);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }
        return Success(result.Data);
    }
}
