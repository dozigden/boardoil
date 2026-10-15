using System.Globalization;
using System.Text.Json;
using BoardOil.Contracts.CustomField;
using BoardOil.Contracts.Common;

namespace BoardOil.Services.CustomField;

public static class CustomFieldRequestParser
{
    public static CustomFieldValidationResult<CustomFieldDraft> Parse(CustomFieldRequest request)
    {
        var parsed = ParseDefinition(request.Definition);
        if (!parsed.IsValid) { return new(null, parsed.Errors); }
        return new(new(parsed.Value!, request.AppliesTo), []);
    }

    public static CustomFieldValidationResult<IReadOnlyDictionary<string, JsonElement>> ParseValues(JsonElement fields)
    {
        if (fields.ValueKind == JsonValueKind.Undefined)
        {
            return new(null, []);
        }
        if (fields.ValueKind != JsonValueKind.Object)
        {
            return new(null, [new("customFieldValues", "Field values must be an object.")]);
        }

        // Keep raw scalar values for the shared field validator; do not coerce strings or round numbers.
        var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<ValidationError>();
        foreach (var property in fields.EnumerateObject())
        {
            if (!values.TryAdd(property.Name, property.Value.Clone()))
            {
                errors.Add(new($"customFieldValues.{property.Name}", "Field code is repeated, ignoring case."));
            }
        }
        return new(values, errors);
    }

    private static CustomFieldValidationResult<CustomFieldDefinition> ParseDefinition(CustomFieldDefinitionInput input)
    {
        var errors = new List<ValidationError>();
        const string path = "definition";
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
                return new(null, [new(path, "Unsupported type")]);
        }
        if (errors.Count > 0) { return new(null, errors); }
        return new(definition with { Emoji = input.Emoji, Nullable = input.Nullable, ShowOnCard = input.ShowOnCard }, []);
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
