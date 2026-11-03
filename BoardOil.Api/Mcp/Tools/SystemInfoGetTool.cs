using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Users;
using BoardOil.Api.Configuration;
using BoardOil.Contracts.Support;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed record SystemInfoGetOutput(BoardOilBuildInfo Application, DatabaseInfoDto Database);

public sealed class SystemInfoGetTool(
    BoardOilBuildInfo buildInfo,
    IDatabaseInfoProvider databaseInfo,
    IUserService users,
    IMcpAuthorisationService authorisationService)
    : McpToolBase<SystemInfoGetInput, SystemInfoGetOutput>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.SystemInfoGet,
        "Get the deployed application version, channel, build and commit, plus database provider, version, journal mode and allocated size in bytes. Available to any active MCP identity; no administrator role or diagnostic permission is required.",
        ToolSchemas.SystemInfoGetInput, ToolSchemas.SystemInfoGetOutput, RequiredScope: null,
        ToolDiscoveryOrder.SystemInfoGet, McpToolBehaviours.ReadOnly);

    protected override async Task<McpToolResult<SystemInfoGetOutput>> ExecuteCoreAsync(
        McpInvocationContext context, SystemInfoGetInput input, CancellationToken cancellationToken)
    {
        var identity = await users.GetCurrentIdentityAsync(context.ActorUserId);
        if (!identity.Success || identity.Data is null)
        {
            return Failure(identity.ToMcpError());
        }

        return Success(new SystemInfoGetOutput(buildInfo, await databaseInfo.GetAsync(cancellationToken)));
    }
}
