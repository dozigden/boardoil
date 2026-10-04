using System.Globalization;
using System.Text.Json;
using BoardOil.Contracts.CardField;
using BoardOil.Contracts.Common;

namespace BoardOil.Services.CardField;

public static class CardFieldConverter
{
    public static CardFieldRuleResult<CardFieldConversionResult> ConvertBetweenTypes(
        IReadOnlyList<CardFieldDefinition> sourceDefinitions,
        IReadOnlyDictionary<string, JsonElement> sourceValues,
        IReadOnlyList<CardFieldDefinition> destinationDefinitions) =>
        Convert(sourceDefinitions, sourceValues, destinationDefinitions, editingDefinitions: false, matchByCode: false);

    public static CardFieldRuleResult<CardFieldConversionResult> ApplyDefinitionChanges(
        IReadOnlyList<CardFieldDefinition> sourceDefinitions,
        IReadOnlyDictionary<string, JsonElement> sourceValues,
        IReadOnlyList<CardFieldDefinition> destinationDefinitions) =>
        Convert(sourceDefinitions, sourceValues, destinationDefinitions, editingDefinitions: true, matchByCode: true);

    public static CardFieldRuleResult<CardFieldConversionResult> Restore(
        IReadOnlyList<CardFieldDefinition> sourceDefinitions,
        IReadOnlyDictionary<string, JsonElement> sourceValues,
        IReadOnlyList<CardFieldDefinition> destinationDefinitions,
        bool sameType) =>
        Convert(sourceDefinitions, sourceValues, destinationDefinitions, editingDefinitions: false, matchByCode: sameType);

    private static CardFieldRuleResult<CardFieldConversionResult> Convert(
        IReadOnlyList<CardFieldDefinition> sourceDefinitions,
        IReadOnlyDictionary<string, JsonElement> sourceValues,
        IReadOnlyList<CardFieldDefinition> destinationDefinitions,
        bool editingDefinitions,
        bool matchByCode)
    {
        var source = CardFieldRules.NormaliseDefinitions(sourceDefinitions);
        var destination = CardFieldRules.NormaliseDefinitions(destinationDefinitions);
        if (!source.IsValid || !destination.IsValid)
        {
            return new(null, [..source.Errors, ..destination.Errors]);
        }
        var sourceValueResult = CardFieldRules.NormaliseValues(source.Value!, sourceValues);
        if (!sourceValueResult.IsValid)
        {
            return new(null, sourceValueResult.Errors);
        }

        var sourceFields = source.Value!;
        var originalValues = sourceValueResult.Value!;
        var sourcesByIdentity = sourceFields.ToDictionary(
            field => MatchIdentity(field, matchByCode), StringComparer.OrdinalIgnoreCase);
        var preserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<ValidationError>();
        foreach (var target in destination.Value!)
        {
            if (!sourcesByIdentity.TryGetValue(MatchIdentity(target, matchByCode), out var original))
            {
                // Adding a nullable definition to existing cards differs from creating/converting a card.
                var initialValue = CardFieldRules.DefaultValue(target);
                if (editingDefinitions && target.Nullable)
                {
                    initialValue = CardFieldRules.NullValue;
                }
                values.Add(target.FieldCode, initialValue);
                continue;
            }

            var originalValue = originalValues[original.FieldCode];
            var candidate = ConvertValue(original, target, originalValue);
            if (candidate.HasValue)
            {
                var checkedValue = CardFieldRules.NormaliseValue(target, candidate.Value, $"fields.{target.FieldCode}");
                if (checkedValue.Error is null)
                {
                    values.Add(target.FieldCode, checkedValue.Value);
                    preserved.Add(original.FieldCode);
                    continue;
                }

                if (editingDefinitions && original.GetType() == target.GetType()
                    && originalValue.ValueKind != JsonValueKind.Null)
                {
                    errors.Add(checkedValue.Error);
                    continue;
                }
            }
            values.Add(target.FieldCode, CardFieldRules.DefaultValue(target));
        }

        if (errors.Count > 0)
        {
            return new(null, errors);
        }

        var losses = sourceFields
            .Where(field => !preserved.Contains(field.FieldCode)
                && originalValues[field.FieldCode].ValueKind != JsonValueKind.Null)
            .Select(field => new LostCardFieldValue(field.DisplayName, CardFieldRules.FormatValue(originalValues[field.FieldCode])))
            .ToArray();
        return new(new(values, losses), []);
    }

    private static string MatchIdentity(CardFieldDefinition definition, bool matchByCode)
    {
        if (matchByCode)
        {
            return definition.FieldCode;
        }
        return definition.DisplayName;
    }

    private static JsonElement? ConvertValue(CardFieldDefinition source, CardFieldDefinition target, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null || source.GetType() == target.GetType())
        {
            return value;
        }
        if (target is TextFieldDefinition && source is IntegerFieldDefinition or DateFieldDefinition)
        {
            return JsonSerializer.SerializeToElement(CardFieldRules.FormatValue(value));
        }
        if (source is TextFieldDefinition && target is IntegerFieldDefinition
            && int.TryParse(value.GetString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
        {
            return JsonSerializer.SerializeToElement(number);
        }
        return null;
    }
}
