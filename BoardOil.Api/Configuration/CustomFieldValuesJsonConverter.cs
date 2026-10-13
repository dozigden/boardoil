using System.Text.Json;
using System.Text.Json.Serialization;
using BoardOil.Services.CustomField;

namespace BoardOil.Api.Configuration;

// Validate the dictionary structure here; preserve submitted values for the shared field rules.
public sealed class CustomFieldValuesJsonConverter : JsonConverter<IReadOnlyDictionary<string, JsonElement>>
{
    public override bool HandleNull => true;

    public override IReadOnlyDictionary<string, JsonElement> Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Field values must be an object.");
        }
        var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!values.TryAdd(property.Name, property.Value.Clone()))
            {
                throw new JsonException($"Field code '{property.Name}' is repeated.");
            }
        }
        return values;
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyDictionary<string, JsonElement> value, JsonSerializerOptions options)
    {
        writer.WriteRawValue(CustomFieldJsonCodec.SerialiseValues(value));
    }
}
