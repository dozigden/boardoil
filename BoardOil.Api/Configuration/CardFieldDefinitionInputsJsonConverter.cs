using System.Text.Json;
using System.Text.Json.Serialization;
using BoardOil.Contracts.CardField;
using BoardOil.Services.CardField;

namespace BoardOil.Api.Configuration;

public sealed class CardFieldDefinitionInputsJsonConverter : JsonConverter<IReadOnlyList<CardFieldDefinitionInput>>
{
    public override bool HandleNull => true;

    public override IReadOnlyList<CardFieldDefinitionInput> Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return CardFieldJsonCodec.DeserialiseDefinitionInputs(document.RootElement.GetRawText());
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<CardFieldDefinitionInput> value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value.ToArray(), options);
}
