using System.Text.Json;
using System.Text.Json.Serialization;
using BoardOil.Contracts.CardField;

namespace BoardOil.Services.CardField;

// Checks JSON structure only. Definition constraints and value conversion belong to field rules.
public static class CardFieldJsonCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.Strict,
        AllowOutOfOrderMetadataProperties = true,
        AllowDuplicateProperties = false,
        RespectNullableAnnotations = true
    };

    public static IReadOnlyList<CardFieldDefinition> DeserialiseDefinitions(string json)
    {
        List<CardFieldDefinition> definitions;
        try
        {
            definitions = JsonSerializer.Deserialize<List<CardFieldDefinition>>(json, JsonOptions)
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

    public static string SerialiseDefinitions(IReadOnlyList<CardFieldDefinition> definitions)
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

    private static void ValidateDefinitionShapes(IReadOnlyList<CardFieldDefinition> definitions)
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
        var fieldCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (fieldCode, value) in values)
        {
            if (!fieldCodes.Add(fieldCode))
            {
                throw new JsonException($"Field code '{fieldCode}' is repeated.");
            }

            if (value.ValueKind is JsonValueKind.Null or JsonValueKind.String)
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _))
            {
                continue;
            }

            throw new JsonException($"Value for field '{fieldCode}' must be a string, a 32-bit integer, or null.");
        }
    }
}
