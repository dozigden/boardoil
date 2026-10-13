using System.Globalization;
using System.Text.Json;
using BoardOil.Contracts.CustomField;
using BoardOil.Contracts.Common;

namespace BoardOil.Services.CustomField;

public static class CustomFieldDefinitionInputs
{
    public static CustomFieldRuleResult<IReadOnlyList<CustomFieldDefinition>> Parse(
        IReadOnlyList<CustomFieldDefinitionInput>? inputs)
    {
        if (inputs is null) { return new(null, []); }

        var errors = new List<ValidationError>();
        var definitions = new List<CustomFieldDefinition>();
        for (var index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index];
            var path = $"customFields[{index}]";
            CustomFieldDefinition definition;
            switch (input)
            {
                case TextCustomFieldDefinitionInput text:
                    definition = new TextCustomFieldDefinition
                    {
                        Code = text.Code, Name = text.Name,
                        DefaultValue = text.DefaultValue,
                        MinLength = Number(text.MinLength, $"{path}.minLength", errors),
                        MaxLength = Number(text.MaxLength, $"{path}.maxLength", errors)
                    };
                    break;
                case IntegerCustomFieldDefinitionInput integer:
                    definition = new IntegerCustomFieldDefinition
                    {
                        Code = integer.Code, Name = integer.Name,
                        DefaultValue = Number(integer.DefaultValue, $"{path}.defaultValue", errors),
                        Min = Number(integer.Min, $"{path}.min", errors),
                        Max = Number(integer.Max, $"{path}.max", errors)
                    };
                    break;
                case DateCustomFieldDefinitionInput date:
                    definition = new DateCustomFieldDefinition
                    {
                        Code = date.Code, Name = date.Name,
                        DefaultValue = Date(date.DefaultValue, $"{path}.defaultValue", errors)
                    };
                    break;
                default:
                    errors.Add(new(path, "Unsupported type"));
                    continue;
            }
            definitions.Add(definition with { Emoji = input.Emoji, Nullable = input.Nullable, ShowOnCard = input.ShowOnCard });
        }

        if (errors.Count > 0) { return new(null, errors); }
        return new(definitions, []);
    }

    private static int? Number(JsonElement? input, string path, List<ValidationError> errors)
    {
        if (input is null || input.Value.ValueKind == JsonValueKind.Null) { return null; }
        var value = input.Value;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var integer)) { return integer; }

        var message = "Whole number required";
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            if (number > int.MaxValue) { message = $"{int.MaxValue} maximum"; }
            else if (number < int.MinValue) { message = $"{int.MinValue} minimum"; }
        }
        errors.Add(new(path, message));
        return null;
    }

    private static DateOnly? Date(JsonElement? input, string path, List<ValidationError> errors)
    {
        if (input is null || input.Value.ValueKind == JsonValueKind.Null) { return null; }
        var value = input.Value;
        if (value.ValueKind == JsonValueKind.String
            && DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }
        errors.Add(new(path, "Date in YYYY-MM-DD format required"));
        return null;
    }
}
