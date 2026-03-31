using BoardOil.Contracts.Contracts;
using BoardOil.Mcp.Contracts;
using ModelContextProtocol.Protocol;
using System.Text.Json;

namespace BoardOil.Api.Mcp;

internal static class McpToolCallHelpers
{
    private static readonly JsonSerializerOptions SerialiserOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<CallToolResult> InvokeAsync<TInput, TOutput>(
        IDictionary<string, JsonElement>? arguments,
        CancellationToken cancellationToken,
        Func<TInput, Task<McpToolResult<TOutput>>> handler)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var inputParseResult = ParseArguments<TInput>(arguments);
        if (!inputParseResult.Success || inputParseResult.Input is null)
        {
            return CreateCallToolResult(inputParseResult.ErrorResult);
        }

        McpToolResult<TOutput> result;
        try
        {
            result = await handler(inputParseResult.Input);
        }
        catch (Exception ex)
        {
            result = new McpToolResult<TOutput>(
                false,
                default,
                new McpToolError("service_error", $"Tool execution failed: {ex.Message}", 500));
        }

        return CreateCallToolResult(result);
    }

    public static CallToolResult CreateCallToolResult<TPayload>(McpToolResult<TPayload> result)
    {
        var payloadJson = JsonSerializer.SerializeToElement(result, SerialiserOptions);
        var text = result.Success ? "ok" : result.Error?.Message ?? "Tool call failed.";

        return new CallToolResult
        {
            IsError = !result.Success,
            StructuredContent = payloadJson,
            Content =
            [
                new TextContentBlock
                {
                    Text = text
                }
            ]
        };
    }

    public static JsonElement ParseJson(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    public static ApiError? ValidateRequiredIdentifier(int? value, string fieldName)
    {
        if (value is > 0)
        {
            return null;
        }

        return ApiErrors.BadRequest(
            "Validation failed.",
            [new ValidationError(fieldName, $"'{fieldName}' is required and must be greater than zero.")]);
    }

    public static ApiError? ValidateOptionalIdentifier(int? value, string fieldName)
    {
        if (value is null || value > 0)
        {
            return null;
        }

        return ApiErrors.BadRequest(
            "Validation failed.",
            [new ValidationError(fieldName, $"'{fieldName}' must be greater than zero when provided.")]);
    }

    private static (bool Success, TInput? Input, McpToolResult<object> ErrorResult) ParseArguments<TInput>(IDictionary<string, JsonElement>? arguments)
    {
        try
        {
            var argumentsJson = JsonSerializer.Serialize(arguments ?? new Dictionary<string, JsonElement>(), SerialiserOptions);
            var parsed = JsonSerializer.Deserialize<TInput>(argumentsJson, SerialiserOptions);
            if (parsed is null)
            {
                return (
                    false,
                    default,
                    new McpToolResult<object>(
                        false,
                        null,
                        new McpToolError("validation_failed", "Tool arguments are required.", 400)));
            }

            return (true, parsed, new McpToolResult<object>(true, null, null));
        }
        catch (JsonException ex)
        {
            return (
                false,
                default,
                new McpToolResult<object>(
                    false,
                    null,
                    new McpToolError("validation_failed", $"Invalid tool arguments: {ex.Message}", 400)));
        }
    }
}
