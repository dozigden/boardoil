using System.Text.Json;
using System.Text.Json.Serialization;
using BoardOil.Services.CardField;

namespace BoardOil.Api.Configuration;

// Keep REST field dictionaries as strict as persisted JSON, including duplicate-key rejection.
public sealed class CardFieldValuesJsonConverter : JsonConverter<IReadOnlyDictionary<string, JsonElement>>
{
    public override bool HandleNull => true;

    public override IReadOnlyDictionary<string, JsonElement> Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return CardFieldJsonCodec.DeserialiseValues(document.RootElement.GetRawText());
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyDictionary<string, JsonElement> value, JsonSerializerOptions options)
    {
        writer.WriteRawValue(CardFieldJsonCodec.SerialiseValues(value));
    }
}
