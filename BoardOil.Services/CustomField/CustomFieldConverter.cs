using System.Globalization;
using System.Text.Json;
using BoardOil.Contracts.CustomField;

namespace BoardOil.Services.CustomField;

public static class CustomFieldConverter
{
    public static CustomFieldRuleResult<CustomFieldConversionResult> ChangeType(
        IReadOnlyList<CustomFieldDefinition> sourceDefinitions,
        IReadOnlyDictionary<string, JsonElement> sourceValues,
        IReadOnlyList<CustomFieldDefinition> destinationDefinitions) =>
        Convert(sourceDefinitions, sourceValues, destinationDefinitions, editingDefinitions: false, matchByCode: true);

    public static CustomFieldRuleResult<CustomFieldConversionResult> ConvertBetweenTypes(
        IReadOnlyList<CustomFieldDefinition> sourceDefinitions,
        IReadOnlyDictionary<string, JsonElement> sourceValues,
        IReadOnlyList<CustomFieldDefinition> destinationDefinitions) =>
        Convert(sourceDefinitions, sourceValues, destinationDefinitions, editingDefinitions: false, matchByCode: false);

    public static CustomFieldRuleResult<CustomFieldConversionResult> ApplyDefinitionChanges(
        IReadOnlyList<CustomFieldDefinition> sourceDefinitions,
        IReadOnlyDictionary<string, JsonElement> sourceValues,
        IReadOnlyList<CustomFieldDefinition> destinationDefinitions) =>
        Convert(sourceDefinitions, sourceValues, destinationDefinitions, editingDefinitions: true, matchByCode: true);

    private static CustomFieldRuleResult<CustomFieldConversionResult> Convert(
        IReadOnlyList<CustomFieldDefinition> sourceDefinitions,
        IReadOnlyDictionary<string, JsonElement> sourceValues,
        IReadOnlyList<CustomFieldDefinition> destinationDefinitions,
        bool editingDefinitions,
        bool matchByCode)
    {
        var source = CustomFieldRules.NormaliseDefinitions(sourceDefinitions);
        var destination = CustomFieldRules.NormaliseDefinitions(destinationDefinitions);
        if (!source.IsValid || !destination.IsValid)
        {
            return new(null, [..source.Errors, ..destination.Errors]);
        }
        var sourceValueResult = CustomFieldRules.NormaliseValues(source.Value!, sourceValues);
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
        foreach (var target in destination.Value!)
        {
            if (!sourcesByIdentity.TryGetValue(MatchIdentity(target, matchByCode), out var original))
            {
                // Adding a nullable definition to existing cards differs from creating/converting a card.
                var initialValue = CustomFieldRules.DefaultValue(target);
                if (editingDefinitions && target.Nullable)
                {
                    initialValue = CustomFieldRules.NullValue;
                }
                values.Add(target.Code, initialValue);
                continue;
            }

            var originalValue = originalValues[original.Code];
            var candidate = ConvertValue(original, target, originalValue);
            if (candidate.HasValue)
            {
                var checkedValue = CustomFieldRules.NormaliseValue(target, candidate.Value, $"customFieldValues.{target.Code}");
                if (checkedValue.Error is null)
                {
                    values.Add(target.Code, checkedValue.Value);
                    preserved.Add(original.Code);
                    continue;
                }
            }
            values.Add(target.Code, CustomFieldRules.DefaultValue(target));
        }

        var losses = sourceFields
            .Where(field => !preserved.Contains(field.Code)
                && originalValues[field.Code].ValueKind != JsonValueKind.Null)
            .Select(field => new LostCustomFieldValue(field.Name, CustomFieldRules.FormatValue(originalValues[field.Code])))
            .ToArray();
        return new(new(values, losses), []);
    }

    private static string MatchIdentity(CustomFieldDefinition definition, bool matchByCode)
    {
        if (matchByCode)
        {
            return definition.Code;
        }
        return definition.Name;
    }

    private static JsonElement? ConvertValue(CustomFieldDefinition source, CustomFieldDefinition target, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null || source.GetType() == target.GetType())
        {
            return value;
        }
        if (target is TextCustomFieldDefinition && source is IntegerCustomFieldDefinition or DateCustomFieldDefinition)
        {
            return JsonSerializer.SerializeToElement(CustomFieldRules.FormatValue(value));
        }
        if (source is TextCustomFieldDefinition && target is IntegerCustomFieldDefinition
            && int.TryParse(value.GetString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
        {
            return JsonSerializer.SerializeToElement(number);
        }
        return null;
    }
}
