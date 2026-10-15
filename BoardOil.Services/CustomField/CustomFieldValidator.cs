using System.Globalization;
using System.Text.Json;
using BoardOil.Contracts.CustomField;
using BoardOil.Contracts.Common;
using BoardOil.Services.Tag;

namespace BoardOil.Services.CustomField;

public static class CustomFieldValidator
{
    public const int MaximumFieldCount = 10;
    public const int MaximumNameLength = 40;
    public const int MaximumTextLength = 250;

    public static CustomFieldValidationResult<IReadOnlyList<CustomFieldDraft>> ValidateAndNormaliseDrafts(
        IReadOnlyList<CustomFieldDraft> fields, IReadOnlySet<int> cardTypeIds)
    {
        var definitions = ValidateAndNormaliseDefinitions(fields.Select(field => field.Definition).ToArray());
        var errors = new List<ValidationError>(definitions.Errors.Select(error =>
            new ValidationError(DefinitionPath(error.Property), error.Message)));
        for (var index = 0; index < fields.Count; index++)
        {
            var path = $"customFields[{index}].appliesTo";
            switch (fields[index].AppliesTo)
            {
                case AllCardTypes:
                    break;
                case SelectedCardTypes selected when selected.CardTypeIds is not null:
                    if (selected.CardTypeIds.Count != selected.CardTypeIds.Distinct().Count())
                    {
                        errors.Add(new($"{path}.cardTypeIds", "Repeated card type"));
                    }
                    if (selected.CardTypeIds.Any(id => !cardTypeIds.Contains(id)))
                    {
                        errors.Add(new($"{path}.cardTypeIds", "Card type does not exist in board"));
                    }
                    break;
                default:
                    errors.Add(new(path, "Required"));
                    break;
            }
        }
        if (errors.Count > 0) { return new(null, errors); }
        return new(fields.Select((field, index) => field with { Definition = definitions.Value![index] }).ToArray(), []);
    }

    private static string DefinitionPath(string path)
    {
        var end = path.IndexOf(']');
        return end < 0 ? path : path.Insert(end + 1, ".definition");
    }

    public static CustomFieldValidationResult<IReadOnlyList<CustomFieldDefinition>> ValidateAndNormaliseDefinitions(
        IReadOnlyList<CustomFieldDefinition> definitions)
    {
        var errors = new List<ValidationError>();
        if (definitions.Count > MaximumFieldCount)
        {
            errors.Add(new("customFields", $"{MaximumFieldCount} fields maximum"));
        }

        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalised = new List<CustomFieldDefinition>();
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            var path = $"customFields[{index}]";
            if (definition is null)
            {
                errors.Add(new(path, "Required"));
                continue;
            }

            var code = definition.Code?.Trim() ?? string.Empty;
            var name = definition.Name?.Trim() ?? string.Empty;
            if (code.Length == 0)
            {
                errors.Add(new($"{path}.code", "Required"));
            }
            else if (code.Length > MaximumNameLength)
            {
                errors.Add(new($"{path}.code", $"{MaximumNameLength} characters maximum"));
            }
            else if (!code.All(char.IsAsciiLetterOrDigit))
            {
                errors.Add(new($"{path}.code", "Characters A-Z, a-z, 0-9 only"));
            }
            else if (!codes.Add(code))
            {
                errors.Add(new($"{path}.code", "Code already used"));
            }
            if (name.Length == 0)
            {
                errors.Add(new($"{path}.name", "Required"));
            }
            else if (name.Length > MaximumNameLength)
            {
                errors.Add(new($"{path}.name", $"{MaximumNameLength} characters maximum"));
            }
            else if (HasLineBreak(name))
            {
                errors.Add(new($"{path}.name", "Single line only"));
            }
            else if (!names.Add(name))
            {
                errors.Add(new($"{path}.name", "Name already used"));
            }

            var emoji = TagEmojiValidator.ValidateAndNormalise(definition.Emoji, $"{path}.emoji");
            if (emoji.Error is not null)
            {
                errors.Add(new(emoji.Error.Property, "Single emoji only"));
            }

            var field = definition with { Code = code, Name = name, Emoji = emoji.CanonicalEmoji };
            switch (field)
            {
                case TextCustomFieldDefinition text:
                    if (text.MinLength is < 0 or > MaximumTextLength)
                    {
                        errors.Add(new($"{path}.minLength", $"0–{MaximumTextLength} only"));
                    }
                    if (text.MaxLength is < 0 or > MaximumTextLength)
                    {
                        errors.Add(new($"{path}.maxLength", $"0–{MaximumTextLength} only"));
                    }
                    if (text.MinLength > text.MaxLength)
                    {
                        errors.Add(new($"{path}.maxLength", "Must be at least the minimum"));
                    }
                    field = text with { DefaultValue = NormaliseText(text.DefaultValue) };
                    break;
                case IntegerCustomFieldDefinition integer:
                    if (integer.Min > integer.Max)
                    {
                        errors.Add(new($"{path}.max", "Must be at least the minimum"));
                    }
                    break;
                case DateCustomFieldDefinition:
                    break;
                default:
                    errors.Add(new($"{path}.dataType", "Unsupported type"));
                    continue;
            }

            var defaultValidation = ValidateAndNormaliseValue(field, CustomFieldJsonCodec.SerialiseDefaultValue(field), $"{path}.defaultValue");
            if (defaultValidation.Error is not null)
            {
                errors.Add(defaultValidation.Error);
            }
            normalised.Add(field);
        }

        if (errors.Count > 0)
        {
            return new(null, errors);
        }
        return new(normalised, []);
    }

    public static CustomFieldValidationResult<IReadOnlyDictionary<string, JsonElement>> ValidateAndNormaliseValues(
        IReadOnlyList<CustomFieldDefinition> definitions,
        IReadOnlyDictionary<string, JsonElement> suppliedValues)
    {
        var definitionResult = ValidateAndNormaliseDefinitions(definitions);
        if (!definitionResult.IsValid)
        {
            return new(null, definitionResult.Errors);
        }

        var fields = definitionResult.Value!;
        var knownCodes = fields.Select(field => field.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var supplied = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<ValidationError>();
        foreach (var (code, value) in suppliedValues)
        {
            if (!supplied.TryAdd(code, value))
            {
                errors.Add(new($"customFieldValues.{code}", "Field code is repeated, ignoring case."));
            }
            if (!knownCodes.Contains(code))
            {
                errors.Add(new($"customFieldValues.{code}", "Field is not defined for this card type."));
            }
        }

        var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            if (!supplied.TryGetValue(field.Code, out var value))
            {
                errors.Add(new($"customFieldValues.{field.Code}", "Field must be supplied, including an explicit null when unset."));
                continue;
            }

            var result = ValidateAndNormaliseValue(field, value, $"customFieldValues.{field.Code}");
            if (result.Error is not null)
            {
                errors.Add(result.Error);
                continue;
            }
            values.Add(field.Code, result.Value);
        }

        if (errors.Count > 0)
        {
            return new(null, errors);
        }
        return new(values, []);
    }

    internal static (JsonElement Value, ValidationError? Error) ValidateAndNormaliseValue(
        CustomFieldDefinition definition, JsonElement value, string path)
    {
        if (definition is TextCustomFieldDefinition && value.ValueKind == JsonValueKind.String)
        {
            value = JsonSerializer.SerializeToElement(NormaliseText(value.GetString()));
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            if (definition.Nullable)
            {
                return (value, null);
            }
            return (default, new(path, "Required"));
        }

        switch (definition)
        {
            case TextCustomFieldDefinition text:
                if (value.ValueKind != JsonValueKind.String)
                {
                    return (default, new(path, "Text only"));
                }
                var content = value.GetString()!;
                if (HasLineBreak(content))
                {
                    return (default, new(path, "Single line only"));
                }
                if (content.Length < (text.MinLength ?? 0))
                {
                    return (default, new(path, $"{text.MinLength} characters minimum"));
                }
                if (content.Length > (text.MaxLength ?? MaximumTextLength))
                {
                    return (default, new(path, $"{text.MaxLength ?? MaximumTextLength} characters maximum"));
                }
                return (value, null);
            case IntegerCustomFieldDefinition integer:
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
                {
                    return (default, new(path, "Whole number required"));
                }
                if (number < integer.Min)
                {
                    return (default, new(path, $"{integer.Min} minimum"));
                }
                if (number > integer.Max)
                {
                    return (default, new(path, $"{integer.Max} maximum"));
                }
                return (JsonSerializer.SerializeToElement(number), null);
            case DateCustomFieldDefinition:
                if (value.ValueKind != JsonValueKind.String
                    || !DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    return (default, new(path, "Date in YYYY-MM-DD format required"));
                }
                return (JsonSerializer.SerializeToElement(date), null);
            default:
                return (default, new(path, "Unsupported type"));
        }
    }

    private static string? NormaliseText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        return value.Trim();
    }

    private static bool HasLineBreak(string value) => value.IndexOfAny(['\r', '\n', '\u0085', '\u2028', '\u2029']) >= 0;
}
