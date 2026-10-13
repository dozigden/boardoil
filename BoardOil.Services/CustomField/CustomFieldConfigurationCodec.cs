using System.Text.Json;
using System.Text.Json.Serialization;
using BoardOil.Contracts.CustomField;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Services.CustomField;

public static class CustomFieldConfigurationCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        AllowDuplicateProperties = false,
        RespectNullableAnnotations = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static CustomFieldDefinition Read(EntityCustomField field)
    {
        CustomFieldDefinition definition;
        switch (field.DataType)
        {
            case CustomFieldDataType.Text:
                var text = Read<TextConfiguration>(field.ConfigurationJson);
                definition = new TextCustomFieldDefinition
                {
                    Code = field.Code, Name = field.Name,
                    DefaultValue = text.DefaultValue, MinLength = text.MinLength, MaxLength = text.MaxLength
                };
                break;
            case CustomFieldDataType.Integer:
                var integer = Read<IntegerConfiguration>(field.ConfigurationJson);
                definition = new IntegerCustomFieldDefinition
                {
                    Code = field.Code, Name = field.Name,
                    DefaultValue = integer.DefaultValue, Min = integer.Min, Max = integer.Max
                };
                break;
            case CustomFieldDataType.Date:
                var date = Read<DateConfiguration>(field.ConfigurationJson);
                definition = new DateCustomFieldDefinition
                {
                    Code = field.Code, Name = field.Name, DefaultValue = date.DefaultValue
                };
                break;
            default: throw new JsonException("Unsupported field type.");
        }
        return definition with { Emoji = field.Emoji, Nullable = field.Nullable, ShowOnCard = field.ShowOnCard };
    }

    public static void Write(EntityCustomField field, CustomFieldDefinition definition)
    {
        field.Code = definition.Code;
        field.Name = definition.Name;
        field.Emoji = definition.Emoji;
        field.Nullable = definition.Nullable;
        field.ShowOnCard = definition.ShowOnCard;
        switch (definition)
        {
            case TextCustomFieldDefinition text:
                field.DataType = CustomFieldDataType.Text;
                field.ConfigurationJson = JsonSerializer.Serialize(new TextConfiguration(text.DefaultValue, text.MinLength, text.MaxLength), JsonOptions);
                break;
            case IntegerCustomFieldDefinition integer:
                field.DataType = CustomFieldDataType.Integer;
                field.ConfigurationJson = JsonSerializer.Serialize(new IntegerConfiguration(integer.DefaultValue, integer.Min, integer.Max), JsonOptions);
                break;
            case DateCustomFieldDefinition date:
                field.DataType = CustomFieldDataType.Date;
                field.ConfigurationJson = JsonSerializer.Serialize(new DateConfiguration(date.DefaultValue), JsonOptions);
                break;
            default: throw new JsonException("Unsupported field type.");
        }
    }

    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions)
        ?? throw new JsonException("Field configuration must be an object.");

    private sealed record TextConfiguration(string? DefaultValue, int? MinLength, int? MaxLength);
    private sealed record IntegerConfiguration(int? DefaultValue, int? Min, int? Max);
    private sealed record DateConfiguration(DateOnly? DefaultValue);
}
