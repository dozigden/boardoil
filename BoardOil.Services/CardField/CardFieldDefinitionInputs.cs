using System.Globalization;
using System.Text.Json;
using BoardOil.Contracts.CardField;
using BoardOil.Contracts.Common;

namespace BoardOil.Services.CardField;

public static class CardFieldDefinitionInputs
{
    public static CardFieldRuleResult<IReadOnlyList<CardFieldDefinition>> Parse(
        IReadOnlyList<CardFieldDefinitionInput>? inputs)
    {
        if (inputs is null) { return new(null, []); }

        var errors = new List<ValidationError>();
        var definitions = new List<CardFieldDefinition>();
        for (var index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index];
            var path = $"fields[{index}]";
            CardFieldDefinition definition;
            switch (input)
            {
                case TextFieldDefinitionInput text:
                    definition = new TextFieldDefinition
                    {
                        FieldCode = text.FieldCode, DisplayName = text.DisplayName,
                        DefaultValue = text.DefaultValue,
                        MinLength = Number(text.MinLength, $"{path}.minLength", errors),
                        MaxLength = Number(text.MaxLength, $"{path}.maxLength", errors)
                    };
                    break;
                case IntegerFieldDefinitionInput integer:
                    definition = new IntegerFieldDefinition
                    {
                        FieldCode = integer.FieldCode, DisplayName = integer.DisplayName,
                        DefaultValue = Number(integer.DefaultValue, $"{path}.defaultValue", errors),
                        Min = Number(integer.Min, $"{path}.min", errors),
                        Max = Number(integer.Max, $"{path}.max", errors)
                    };
                    break;
                case DateFieldDefinitionInput date:
                    definition = new DateFieldDefinition
                    {
                        FieldCode = date.FieldCode, DisplayName = date.DisplayName,
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
