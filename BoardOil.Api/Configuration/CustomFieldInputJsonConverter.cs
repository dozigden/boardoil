using System.Text.Json;
using System.Text.Json.Serialization;
using BoardOil.Contracts.CustomField;

namespace BoardOil.Api.Configuration;

public sealed class CustomFieldInputJsonConverter : JsonConverter<CustomFieldInput>
{
    private static readonly JsonSerializerOptions InputOptions = new(JsonSerializerDefaults.Web)
    {
        AllowOutOfOrderMetadataProperties = true,
        AllowDuplicateProperties = false,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    };

    public override CustomFieldInput Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
        {
            return JsonSerializer.Deserialize<CustomFieldInput>(ref reader, InputOptions)
                ?? throw new JsonException("Field must be an object.");
        }
        catch (NotSupportedException exception)
        {
            throw new JsonException("Supported field type and applicability required.", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, CustomFieldInput value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, InputOptions);
}
