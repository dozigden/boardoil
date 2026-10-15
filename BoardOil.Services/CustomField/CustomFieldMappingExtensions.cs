using System.Text.Json;
using BoardOil.Contracts.CustomField;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Services.CustomField;

public static class CustomFieldMappingExtensions
{
    public static CustomFieldDto ToCustomFieldDto(this EntityCustomField field) =>
        new(field.Id, field.ToCustomFieldDefinition(), field.ToCustomFieldApplicability());

    public static IReadOnlyList<CustomFieldDto> ToCustomFieldDtos(this IEnumerable<EntityCustomField> fields) =>
        Ordered(fields).Select(field => field.ToCustomFieldDto()).ToArray();

    public static CustomFieldDraft ToCustomFieldDraft(this EntityCustomField field) =>
        new(field.ToCustomFieldDefinition(), field.ToCustomFieldApplicability());

    public static IReadOnlyList<CustomFieldDraft> ToCustomFieldDrafts(this IEnumerable<EntityCustomField> fields) =>
        Ordered(fields).Select(field => field.ToCustomFieldDraft()).ToArray();

    public static CustomFieldApplicability ToCustomFieldApplicability(this EntityCustomField field) =>
        field.AppliesToAllCardTypes ? new AllCardTypes()
            : new SelectedCardTypes(field.CardTypeAssignments.Select(assignment => assignment.CardTypeId).Order().ToArray());

    public static CustomFieldDefinition ToCustomFieldDefinition(this EntityCustomField field)
    {
        CustomFieldDefinition definition;
        switch (field.DataType)
        {
            case CustomFieldDataType.Text:
                var text = CustomFieldJsonCodec.DeserialiseTextConfiguration(field.ConfigurationJson);
                definition = new TextCustomFieldDefinition
                {
                    Code = field.Code, Name = field.Name,
                    DefaultValue = text.DefaultValue, MinLength = text.MinLength, MaxLength = text.MaxLength
                };
                break;
            case CustomFieldDataType.Integer:
                var integer = CustomFieldJsonCodec.DeserialiseIntegerConfiguration(field.ConfigurationJson);
                definition = new IntegerCustomFieldDefinition
                {
                    Code = field.Code, Name = field.Name,
                    DefaultValue = integer.DefaultValue, Min = integer.Min, Max = integer.Max
                };
                break;
            case CustomFieldDataType.Date:
                var date = CustomFieldJsonCodec.DeserialiseDateConfiguration(field.ConfigurationJson);
                definition = new DateCustomFieldDefinition
                {
                    Code = field.Code, Name = field.Name, DefaultValue = date.DefaultValue
                };
                break;
            default: throw new JsonException("Unsupported field type.");
        }
        return definition with { Emoji = field.Emoji, Nullable = field.Nullable, ShowOnCard = field.ShowOnCard };
    }

    public static void ApplyCustomFieldDraft(this EntityCustomField field, CustomFieldDraft draft)
    {
        var definition = draft.Definition;
        field.Code = definition.Code;
        field.Name = definition.Name;
        field.Emoji = definition.Emoji;
        field.Nullable = definition.Nullable;
        field.ShowOnCard = definition.ShowOnCard;
        field.DataType = definition switch
        {
            TextCustomFieldDefinition => CustomFieldDataType.Text,
            IntegerCustomFieldDefinition => CustomFieldDataType.Integer,
            DateCustomFieldDefinition => CustomFieldDataType.Date,
            _ => throw new ArgumentOutOfRangeException(nameof(draft))
        };
        field.ConfigurationJson = CustomFieldJsonCodec.SerialiseConfiguration(definition);
        field.AppliesToAllCardTypes = draft.AppliesTo is AllCardTypes;
        var selected = draft.AppliesTo is SelectedCardTypes types ? types.CardTypeIds.ToHashSet() : [];
        foreach (var removed in field.CardTypeAssignments.Where(assignment => !selected.Contains(assignment.CardTypeId)).ToArray())
        {
            field.CardTypeAssignments.Remove(removed);
        }
        foreach (var typeId in selected.Except(field.CardTypeAssignments.Select(assignment => assignment.CardTypeId)))
        {
            field.CardTypeAssignments.Add(new EntityCustomFieldCardType { BoardId = field.BoardId, CustomField = field, CardTypeId = typeId });
        }
    }

    public static IReadOnlyList<CustomFieldDefinition> GetApplicableCustomFieldDefinitions(
        this IEnumerable<EntityCustomField> fields, int cardTypeId) =>
        Ordered(fields).Where(field => field.AppliesToAllCardTypes
            || field.CardTypeAssignments.Any(assignment => assignment.CardTypeId == cardTypeId))
            .Select(field => field.ToCustomFieldDefinition()).ToArray();

    public static IReadOnlyList<CustomFieldDefinition> GetApplicableCustomFieldDefinitions(
        this IEnumerable<CustomFieldDraft> fields, int cardTypeId) =>
        fields.Where(field => field.AppliesTo.AppliesToCardType(cardTypeId)).Select(field => field.Definition).ToArray();

    public static bool AppliesToCardType(this CustomFieldApplicability applicability, int cardTypeId) => applicability switch
    {
        AllCardTypes => true,
        SelectedCardTypes selected => selected.CardTypeIds.Contains(cardTypeId),
        _ => false
    };

    private static IOrderedEnumerable<EntityCustomField> Ordered(IEnumerable<EntityCustomField> fields) =>
        fields.OrderBy(field => field.SortOrder).ThenBy(field => field.Id);
}
