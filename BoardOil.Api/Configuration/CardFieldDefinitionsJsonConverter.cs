using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using BoardOil.Contracts.CardField;
using BoardOil.Contracts.Common;
using BoardOil.Services.CardField;

namespace BoardOil.Api.Configuration;

public sealed class CardFieldDefinitionsJsonConverter : JsonConverter<IReadOnlyList<CardFieldDefinition>>
{
    public override bool HandleNull => true;

    public override IReadOnlyList<CardFieldDefinition> Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        ValidateScalarInputs(document.RootElement);
        return CardFieldJsonCodec.DeserialiseDefinitions(document.RootElement.GetRawText());
    }

    private static void ValidateScalarInputs(JsonElement fields)
    {
        if (fields.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var errors = new List<ValidationError>();
        var index = 0;
        foreach (var field in fields.EnumerateArray())
        {
            if (field.ValueKind == JsonValueKind.Object
                && field.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String)
            {
                var path = $"fields[{index}]";
                switch (type.GetString())
                {
                    case "integer":
                        ValidateNumber(field, "defaultValue", path, errors);
                        ValidateNumber(field, "min", path, errors);
                        ValidateNumber(field, "max", path, errors);
                        break;
                    case "text":
                        ValidateNumber(field, "minLength", path, errors);
                        ValidateNumber(field, "maxLength", path, errors);
                        break;
                    case "date":
                        if (field.TryGetProperty("defaultValue", out var date) && date.ValueKind != JsonValueKind.Null
                            && (date.ValueKind != JsonValueKind.String
                                || !DateOnly.TryParseExact(date.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))
                        {
                            errors.Add(new($"{path}.defaultValue", "Date in YYYY-MM-DD format required"));
                        }
                        break;
                }
            }
            index++;
        }

        if (errors.Count > 0)
        {
            throw new CardFieldDefinitionValidationException(errors);
        }
    }

    private static void ValidateNumber(JsonElement field, string property, string path, List<ValidationError> errors)
    {
        if (!field.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return;
        }
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _))
        {
            return;
        }

        var message = "Whole number required";
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            if (number > int.MaxValue)
            {
                message = $"{int.MaxValue} maximum";
            }
            else if (number < int.MinValue)
            {
                message = $"{int.MinValue} minimum";
            }
        }
        errors.Add(new($"{path}.{property}", message));
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<CardFieldDefinition> value, JsonSerializerOptions options) =>
        writer.WriteRawValue(CardFieldJsonCodec.SerialiseDefinitions(value));
}
