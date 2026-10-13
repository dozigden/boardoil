using System.Text.Json;
using System.Text.Json.Serialization;
using BoardOil.Contracts.CustomField;

namespace BoardOil.Services.CustomField;

// Checks JSON structure only. Definition constraints and value conversion belong to field rules.
public static class CustomFieldJsonCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.Strict,
        AllowOutOfOrderMetadataProperties = true,
        AllowDuplicateProperties = false,
        RespectNullableAnnotations = true
    };

    public static IReadOnlyList<CustomFieldDefinition> DeserialiseDefinitions(string json)
    {
        List<CustomFieldDefinition> definitions;
        try
        {
            definitions = JsonSerializer.Deserialize<List<CustomFieldDefinition>>(json, JsonOptions)
                ?? throw new JsonException("Field definitions must be an array.");
        }
        catch (NotSupportedException exception)
        {
            // System.Text.Json cannot construct the abstract base when the discriminator is missing.
            throw new JsonException("Each field definition must specify a supported type.", exception);
        }

        ValidateDefinitionShapes(definitions);
        return definitions;
    }

    public static IReadOnlyList<CustomFieldDefinitionInput> DeserialiseDefinitionInputs(string json)
    {
        List<CustomFieldDefinitionInput> inputs;
        try
        {
            inputs = JsonSerializer.Deserialize<List<CustomFieldDefinitionInput>>(json, JsonOptions)
                ?? throw new JsonException("Field definitions must be an array.");
        }
        catch (NotSupportedException exception)
        {
            throw new JsonException("Each field definition must specify a supported type.", exception);
        }
        if (inputs.Any(input => input is null))
        {
            throw new JsonException("Each field definition must be an object.");
        }
        return inputs;
    }

    public static string SerialiseDefinitions(IReadOnlyList<CustomFieldDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ValidateDefinitionShapes(definitions);
        return JsonSerializer.Serialize(definitions, JsonOptions);
    }

    public static IReadOnlyDictionary<string, JsonElement> DeserialiseValues(string json)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)
            ?? throw new JsonException("Field values must be an object.");
        ValidateValueShapes(values);
        return new Dictionary<string, JsonElement>(values, StringComparer.OrdinalIgnoreCase);
    }

    public static string SerialiseValues(IReadOnlyDictionary<string, JsonElement> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        ValidateValueShapes(values);
        return JsonSerializer.Serialize(values, JsonOptions);
    }

    private static void ValidateDefinitionShapes(IReadOnlyList<CustomFieldDefinition> definitions)
    {
        for (var index = 0; index < definitions.Count; index++)
        {
            if (definitions[index] is null)
            {
                throw new JsonException($"Field definition at index {index} must be an object.");
            }
        }
    }

    private static void ValidateValueShapes(IReadOnlyDictionary<string, JsonElement> values)
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (code, value) in values)
        {
            if (!codes.Add(code))
            {
                throw new JsonException($"Field code '{code}' is repeated.");
            }

            if (value.ValueKind is JsonValueKind.Null or JsonValueKind.String)
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _))
            {
                continue;
            }

            throw new JsonException($"Value for field '{code}' must be a string, a 32-bit integer, or null.");
        }
    }
}
