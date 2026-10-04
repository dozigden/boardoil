using System.Text.Json;
using System.Text.Json.Serialization;
using BoardOil.Contracts.CardField;
using BoardOil.Services.CardField;

namespace BoardOil.Api.Configuration;

public sealed class CardFieldDefinitionsJsonConverter : JsonConverter<IReadOnlyList<CardFieldDefinition>>
{
    public override bool HandleNull => true;

    public override IReadOnlyList<CardFieldDefinition> Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return CardFieldJsonCodec.DeserialiseDefinitions(document.RootElement.GetRawText());
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<CardFieldDefinition> value, JsonSerializerOptions options) =>
        writer.WriteRawValue(CardFieldJsonCodec.SerialiseDefinitions(value));
}
