using System.Text.Json;
using BoardOil.Contracts.Common;
using BoardOil.Services.CustomField;

namespace BoardOil.Api.Mcp;

internal static class McpCustomFieldInputs
{
    public static CustomFieldRuleResult<IReadOnlyDictionary<string, JsonElement>> ParseValues(JsonElement fields)
    {
        if (fields.ValueKind == JsonValueKind.Undefined)
        {
            return new(null, []);
        }
        if (fields.ValueKind != JsonValueKind.Object)
        {
            return new(null, [new("customFieldValues", "Field values must be an object.")]);
        }

        // Keep raw scalar values for the shared field rules; do not coerce strings or round numbers.
        var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<ValidationError>();
        foreach (var property in fields.EnumerateObject())
        {
            if (!values.TryAdd(property.Name, property.Value.Clone()))
            {
                errors.Add(new($"customFieldValues.{property.Name}", "Field code is repeated, ignoring case."));
            }
        }
        return new(values, errors);
    }
}
