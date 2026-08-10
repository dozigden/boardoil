using BoardOil.Abstractions.Users;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class IdentityGetTool(
    IUserService userService,
    IMcpAuthorisationService authorisationService) : McpToolBase<IdentityGetInput, IdentityGetOutput>(authorisationService)
{
    private readonly IUserService _userService = userService;

    public override McpToolDefinition Definition { get; } =
        new(
            ToolNames.IdentityGet,
            "Get the BoardOil user and authentication context for the current MCP connection.",
            ToolSchemas.IdentityGetInput,
            ToolSchemas.IdentityGetOutput,
            RequiredScope: null);

    protected override async Task<McpToolResult<IdentityGetOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        IdentityGetInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = await _userService.GetCurrentIdentityAsync(context.ActorUserId);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        var authenticationType = context.AccessContext?.AuthenticationType ?? "None";
        var scopes = context.AccessContext?.Scopes
            .OrderBy(scope => scope, StringComparer.Ordinal)
            .ToArray() ?? [];
        return Success(new IdentityGetOutput(
            new McpIdentityUser(
                result.Data.Id,
                result.Data.UserName,
                result.Data.DisplayName,
                result.Data.Role),
            new McpAuthenticationContext(authenticationType, scopes)));
    }
}
