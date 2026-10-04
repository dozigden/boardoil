using System.Globalization;
using System.Text.Json;
using BoardOil.Contracts.CardField;
using BoardOil.Contracts.Common;
using BoardOil.Services.Tag;

namespace BoardOil.Services.CardField;

public static class CardFieldRules
{
    public const int MaximumFieldCount = 10;
    public const int MaximumNameLength = 40;
    public const int MaximumTextLength = 250;

    internal static JsonElement NullValue => JsonSerializer.SerializeToElement<string?>(null);

    public static CardFieldRuleResult<IReadOnlyList<CardFieldDefinition>> NormaliseDefinitions(
        IReadOnlyList<CardFieldDefinition> definitions)
    {
        var errors = new List<ValidationError>();
        if (definitions.Count > MaximumFieldCount)
        {
            errors.Add(new("fields", $"A card type can have at most {MaximumFieldCount} fields."));
        }

        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalised = new List<CardFieldDefinition>();
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            var path = $"fields[{index}]";
            if (definition is null)
            {
                errors.Add(new(path, "Field definition is required."));
                continue;
            }

            var code = definition.FieldCode?.Trim() ?? string.Empty;
            var name = definition.DisplayName?.Trim() ?? string.Empty;
            if (code.Length is 0 or > MaximumNameLength || !code.All(char.IsAsciiLetterOrDigit))
            {
                errors.Add(new($"{path}.fieldCode", $"Field code must contain 1–{MaximumNameLength} letters or digits; underscores are reserved for system fields."));
            }
            if (!codes.Add(code))
            {
                errors.Add(new($"{path}.fieldCode", "Field codes must be unique, ignoring case."));
            }
            if (name.Length is 0 or > MaximumNameLength || HasLineBreak(name))
            {
                errors.Add(new($"{path}.displayName", $"Display name must contain 1–{MaximumNameLength} characters on a single line."));
            }
            if (!names.Add(name))
            {
                errors.Add(new($"{path}.displayName", "Display names must be unique, ignoring case."));
            }

            var emoji = TagEmojiValidator.ValidateAndNormalise(definition.Emoji, $"{path}.emoji");
            if (emoji.Error is not null)
            {
                errors.Add(emoji.Error);
            }

            var field = definition with { FieldCode = code, DisplayName = name, Emoji = emoji.CanonicalEmoji };
            switch (field)
            {
                case TextFieldDefinition text:
                    if (text.MinLength is < 0 or > MaximumTextLength)
                    {
                        errors.Add(new($"{path}.minLength", $"Minimum length must be between 0 and {MaximumTextLength}."));
                    }
                    if (text.MaxLength is < 0 or > MaximumTextLength)
                    {
                        errors.Add(new($"{path}.maxLength", $"Maximum length must be between 0 and {MaximumTextLength}."));
                    }
                    if (text.MinLength > text.MaxLength)
                    {
                        errors.Add(new($"{path}.maxLength", "Maximum length cannot be less than minimum length."));
                    }
                    field = text with { DefaultValue = NormaliseText(text.DefaultValue) };
                    break;
                case IntegerFieldDefinition integer:
                    if (integer.Min > integer.Max)
                    {
                        errors.Add(new($"{path}.max", "Maximum cannot be less than minimum."));
                    }
                    break;
                case DateFieldDefinition:
                    break;
                default:
                    errors.Add(new($"{path}.type", "Field type is not supported."));
                    continue;
            }

            var defaultValidation = NormaliseValue(field, DefaultValue(field), $"{path}.defaultValue");
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

    public static CardFieldRuleResult<IReadOnlyDictionary<string, JsonElement>> CreateValues(
        IReadOnlyList<CardFieldDefinition> definitions,
        IReadOnlyDictionary<string, JsonElement>? suppliedValues = null) =>
        BuildValues(definitions, suppliedValues ?? new Dictionary<string, JsonElement>(), useDefaultsForMissing: true);

    public static CardFieldRuleResult<IReadOnlyDictionary<string, JsonElement>> NormaliseValues(
        IReadOnlyList<CardFieldDefinition> definitions,
        IReadOnlyDictionary<string, JsonElement> values) =>
        BuildValues(definitions, values, useDefaultsForMissing: false);

    private static CardFieldRuleResult<IReadOnlyDictionary<string, JsonElement>> BuildValues(
        IReadOnlyList<CardFieldDefinition> definitions,
        IReadOnlyDictionary<string, JsonElement> suppliedValues,
        bool useDefaultsForMissing)
    {
        var definitionResult = NormaliseDefinitions(definitions);
        if (!definitionResult.IsValid)
        {
            return new(null, definitionResult.Errors);
        }

        var fields = definitionResult.Value!;
        var knownCodes = fields.Select(field => field.FieldCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var supplied = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<ValidationError>();
        foreach (var (code, value) in suppliedValues)
        {
            if (!supplied.TryAdd(code, value))
            {
                errors.Add(new($"fields.{code}", "Field code is repeated, ignoring case."));
            }
            if (!knownCodes.Contains(code))
            {
                errors.Add(new($"fields.{code}", "Field is not defined for this card type."));
            }
        }

        var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            if (!supplied.TryGetValue(field.FieldCode, out var value))
            {
                if (!useDefaultsForMissing)
                {
                    errors.Add(new($"fields.{field.FieldCode}", "Field must be supplied, including an explicit null when unset."));
                    continue;
                }
                value = DefaultValue(field);
            }

            var result = NormaliseValue(field, value, $"fields.{field.FieldCode}");
            if (result.Error is not null)
            {
                errors.Add(result.Error);
                continue;
            }
            values.Add(field.FieldCode, result.Value);
        }

        if (errors.Count > 0)
        {
            return new(null, errors);
        }
        return new(values, []);
    }

    internal static (JsonElement Value, ValidationError? Error) NormaliseValue(
        CardFieldDefinition definition, JsonElement value, string path)
    {
        if (definition is TextFieldDefinition && value.ValueKind == JsonValueKind.String)
        {
            value = JsonSerializer.SerializeToElement(NormaliseText(value.GetString()));
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            if (definition.Nullable)
            {
                return (NullValue, null);
            }
            return (default, new(path, "A non-null value is required."));
        }

        switch (definition)
        {
            case TextFieldDefinition text:
                if (value.ValueKind != JsonValueKind.String)
                {
                    return (default, new(path, "Value must be text."));
                }
                var content = value.GetString()!;
                if (HasLineBreak(content))
                {
                    return (default, new(path, "Text must be a single line."));
                }
                if (content.Length < (text.MinLength ?? 0) || content.Length > (text.MaxLength ?? MaximumTextLength))
                {
                    return (default, new(path, $"Text length must be between {text.MinLength ?? 0} and {text.MaxLength ?? MaximumTextLength}."));
                }
                return (value, null);
            case IntegerFieldDefinition integer:
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
                {
                    return (default, new(path, "Value must be a 32-bit integer."));
                }
                if (number < integer.Min || number > integer.Max)
                {
                    return (default, new(path, $"Integer must be between {integer.Min ?? int.MinValue} and {integer.Max ?? int.MaxValue}."));
                }
                return (JsonSerializer.SerializeToElement(number), null);
            case DateFieldDefinition:
                if (value.ValueKind != JsonValueKind.String
                    || !DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    return (default, new(path, "Value must be a calendar date in YYYY-MM-DD format."));
                }
                return (JsonSerializer.SerializeToElement(date), null);
            default:
                return (default, new(path, "Field type is not supported."));
        }
    }

    internal static JsonElement DefaultValue(CardFieldDefinition definition) => definition switch
    {
        TextFieldDefinition text => JsonSerializer.SerializeToElement(text.DefaultValue),
        IntegerFieldDefinition integer => JsonSerializer.SerializeToElement(integer.DefaultValue),
        DateFieldDefinition date => JsonSerializer.SerializeToElement(date.DefaultValue),
        _ => throw new ArgumentOutOfRangeException(nameof(definition))
    };

    internal static string FormatValue(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString()!;
        }
        return value.GetRawText();
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
