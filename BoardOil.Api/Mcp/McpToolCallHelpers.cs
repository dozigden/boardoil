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

    public static (bool Success, TInput? Input, McpToolError? Error) ParseArguments<TInput>(IDictionary<string, JsonElement>? arguments)
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
                    new McpToolError("validation_failed", "Tool arguments are required.", 400));
            }

            return (true, parsed, null);
        }
        catch (JsonException ex)
        {
            return (
                false,
                default,
                new McpToolError("validation_failed", $"Invalid tool arguments: {ex.Message}", 400));
        }
    }

    public static CallToolResult CreateSuccessCallToolResult<TPayload>(TPayload payload) =>
        new()
        {
            IsError = false,
            StructuredContent = JsonSerializer.SerializeToElement(payload, SerialiserOptions),
            Content =
            [
                new TextContentBlock
                {
                    Text = "ok"
                }
            ]
        };

    public static CallToolResult CreateErrorCallToolResult(McpToolError error) =>
        new()
        {
            IsError = true,
            StructuredContent = JsonSerializer.SerializeToElement(error, SerialiserOptions),
            Content =
            [
                new TextContentBlock
                {
                    Text = error.Message
                }
            ]
        };

    public static CallToolResult CreateErrorCallToolResult(string code, string message, int statusCode) =>
        CreateErrorCallToolResult(new McpToolError(code, message, statusCode));

    public static McpToolError CreateUnhandledServiceError(Exception exception) =>
        new("service_error", $"Tool execution failed: {exception.Message}", 500);

    public static JsonElement ParseJson(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    public static IReadOnlyList<ValidationError> ValidateRequiredIdentifier(int? value, string fieldName)
    {
        if (value is > 0)
        {
            return [];
        }

        return [new ValidationError(fieldName, $"'{fieldName}' is required and must be greater than zero.")];
    }

    public static IReadOnlyList<ValidationError> ValidateOptionalIdentifier(int? value, string fieldName)
    {
        if (value is null || value > 0)
        {
            return [];
        }

        return [new ValidationError(fieldName, $"'{fieldName}' must be greater than zero when provided.")];
    }

}
