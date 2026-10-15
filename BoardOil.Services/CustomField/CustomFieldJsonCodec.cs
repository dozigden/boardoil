using System.Text.Json;
using System.Text.Json.Serialization;
using BoardOil.Contracts.CustomField;

namespace BoardOil.Services.CustomField;

// Checks JSON structure only. Definition constraints and value conversion are handled separately.
public static class CustomFieldJsonCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.Strict,
        AllowOutOfOrderMetadataProperties = true,
        AllowDuplicateProperties = false,
        RespectNullableAnnotations = true
    };

    private static readonly JsonSerializerOptions ConfigurationOptions = new(JsonSerializerDefaults.Web)
    {
        AllowDuplicateProperties = false,
        RespectNullableAnnotations = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    internal sealed record TextConfiguration(string? DefaultValue, int? MinLength, int? MaxLength);
    internal sealed record IntegerConfiguration(int? DefaultValue, int? Min, int? Max);
    internal sealed record DateConfiguration(DateOnly? DefaultValue);

    internal static TextConfiguration DeserialiseTextConfiguration(string json) => DeserialiseConfiguration<TextConfiguration>(json);
    internal static IntegerConfiguration DeserialiseIntegerConfiguration(string json) => DeserialiseConfiguration<IntegerConfiguration>(json);
    internal static DateConfiguration DeserialiseDateConfiguration(string json) => DeserialiseConfiguration<DateConfiguration>(json);

    internal static string SerialiseConfiguration(CustomFieldDefinition definition) => definition switch
    {
        TextCustomFieldDefinition text => JsonSerializer.Serialize(new TextConfiguration(text.DefaultValue, text.MinLength, text.MaxLength), ConfigurationOptions),
        IntegerCustomFieldDefinition integer => JsonSerializer.Serialize(new IntegerConfiguration(integer.DefaultValue, integer.Min, integer.Max), ConfigurationOptions),
        DateCustomFieldDefinition date => JsonSerializer.Serialize(new DateConfiguration(date.DefaultValue), ConfigurationOptions),
        _ => throw new ArgumentOutOfRangeException(nameof(definition))
    };

    internal static JsonElement SerialiseDefaultValue(CustomFieldDefinition definition) => definition switch
    {
        TextCustomFieldDefinition text => JsonSerializer.SerializeToElement(text.DefaultValue),
        IntegerCustomFieldDefinition integer => JsonSerializer.SerializeToElement(integer.DefaultValue),
        DateCustomFieldDefinition date => JsonSerializer.SerializeToElement(date.DefaultValue),
        _ => throw new ArgumentOutOfRangeException(nameof(definition))
    };

    private static T DeserialiseConfiguration<T>(string json) => JsonSerializer.Deserialize<T>(json, ConfigurationOptions)
        ?? throw new JsonException("Field configuration must be an object.");

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
