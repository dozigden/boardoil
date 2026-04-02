using BoardOil.Contracts.Contracts;
using BoardOil.Mcp.Contracts;
using ModelContextProtocol.Protocol;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoardOil.Api.Mcp;

internal static class McpToolCallHelpers
{
    private static readonly JsonSerializerOptions SerialiserOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly ConcurrentDictionary<Type, IReadOnlySet<string>> AllowedArgumentNamesByInputType = new();

    public static (bool Success, TInput? Input, McpToolError? Error) ParseArguments<TInput>(IDictionary<string, JsonElement>? arguments)
    {
        try
        {
            var providedArguments = arguments ?? new Dictionary<string, JsonElement>();
            var allowedArgumentNames = AllowedArgumentNamesByInputType.GetOrAdd(typeof(TInput), BuildAllowedArgumentNames);
            var unknownArgumentNames = providedArguments.Keys
                .Where(argumentName => !allowedArgumentNames.Contains(argumentName))
                .OrderBy(argumentName => argumentName, StringComparer.Ordinal)
                .ToArray();
            if (unknownArgumentNames.Length > 0)
            {
                return (
                    false,
                    default,
                    new McpToolError(
                        "validation_failed",
                        $"Unknown tool arguments: {string.Join(", ", unknownArgumentNames)}.",
                        400));
            }

            var argumentsJson = JsonSerializer.Serialize(providedArguments, SerialiserOptions);
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

    public static McpToolError CreateUnhandledServiceError(string correlationId) =>
        new("service_error", $"Tool execution failed. Correlation id: {correlationId}.", 500);

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

    private static IReadOnlySet<string> BuildAllowedArgumentNames(Type inputType) =>
        inputType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanWrite && property.GetIndexParameters().Length == 0)
            .Select(ResolveArgumentName)
            .ToHashSet(StringComparer.Ordinal);

    private static string ResolveArgumentName(PropertyInfo propertyInfo)
    {
        var explicitName = propertyInfo.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
        if (!string.IsNullOrWhiteSpace(explicitName))
        {
            return explicitName;
        }

        return SerialiserOptions.PropertyNamingPolicy?.ConvertName(propertyInfo.Name) ?? propertyInfo.Name;
    }

}
