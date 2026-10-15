using System.Text.Json;
using System.Text.Json.Serialization;
using BoardOil.Contracts.CustomField;

namespace BoardOil.Api.Configuration;

public sealed class CustomFieldRequestJsonConverter : JsonConverter<CustomFieldRequest>
{
    private static readonly JsonSerializerOptions InputOptions = new(JsonSerializerDefaults.Web)
    {
        AllowOutOfOrderMetadataProperties = true,
        AllowDuplicateProperties = false,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    };

    public override CustomFieldRequest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
        {
            return JsonSerializer.Deserialize<CustomFieldRequest>(ref reader, InputOptions)
                ?? throw new JsonException("Field must be an object.");
        }
        catch (NotSupportedException exception)
        {
            throw new JsonException("Supported field type and applicability required.", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, CustomFieldRequest value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, InputOptions);
}
