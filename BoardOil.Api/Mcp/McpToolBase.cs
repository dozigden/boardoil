using BoardOil.Mcp.Contracts;
using BoardOil.Contracts.Contracts;
using ModelContextProtocol.Protocol;
using System.Text.Json;

namespace BoardOil.Api.Mcp;

public abstract class McpToolBase<TInput, TOutput>(IMcpAuthorisationService authorisationService) : IMcpTool
{
    protected IMcpAuthorisationService AuthorisationService { get; } = authorisationService;

    public abstract McpToolDefinition Definition { get; }

    public async Task<CallToolResult> ExecuteAsync(
        McpInvocationContext context,
        IDictionary<string, JsonElement>? arguments,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var parseResult = McpToolCallHelpers.ParseArguments<TInput>(arguments);
        if (!parseResult.Success || parseResult.Input is null)
        {
            return McpToolCallHelpers.CreateErrorCallToolResult(
                parseResult.Error ?? new McpToolError("validation_failed", "Tool arguments are required.", 400));
        }

        McpToolResult<TOutput> result;
        try
        {
            result = await ExecuteCoreAsync(context, parseResult.Input, cancellationToken);
        }
        catch (Exception exception)
        {
            return McpToolCallHelpers.CreateErrorCallToolResult(McpToolCallHelpers.CreateUnhandledServiceError(exception));
        }

        if (!result.Success)
        {
            return McpToolCallHelpers.CreateErrorCallToolResult(
                result.Error ?? new McpToolError("service_error", "Tool call failed.", 500));
        }

        if (result.Data is null)
        {
            return McpToolCallHelpers.CreateErrorCallToolResult("service_error", "Tool succeeded without a payload.", 500);
        }

        return McpToolCallHelpers.CreateSuccessCallToolResult(result.Data);
    }

    protected abstract Task<McpToolResult<TOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        TInput input,
        CancellationToken cancellationToken);

    protected static McpToolResult<TOutput> Success(TOutput payload) =>
        new(true, payload, null);

    protected static McpToolResult<TOutput> Failure(McpToolError error) =>
        new(false, default, error);

    protected static McpToolResult<TOutput> Failure(IReadOnlyList<ValidationError> validationErrors)
    {
        var validationMap = validationErrors
            .GroupBy(error => string.IsNullOrWhiteSpace(error.Property) ? string.Empty : error.Property, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(error => error.Message).ToArray(),
                StringComparer.Ordinal);

        return new(
            false,
            default,
            new McpToolError(
                "validation_failed",
                "Validation failed.",
                400,
                validationMap));
    }
}
