using BoardOil.Contracts.CustomField;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Services.CustomField;

public static class CustomFieldCatalogue
{
    public static IReadOnlyList<CustomFieldDto> Read(IEnumerable<EntityCustomField> fields) =>
        fields.OrderBy(field => field.SortOrder).ThenBy(field => field.Id)
            .Select(field => new CustomFieldDto(CustomFieldConfigurationCodec.Read(field),
                field.AppliesToAllCardTypes ? new AllCardTypes()
                    : new SelectedCardTypes(field.CardTypeAssignments.Select(assignment => assignment.CardTypeId).Order().ToArray())))
            .ToArray();

    public static void Apply(EntityBoard board, IReadOnlyList<CustomFieldDto> catalogue)
    {
        var codes = catalogue.Select(field => field.Definition.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var removed in board.CustomFields.Where(field => !codes.Contains(field.Code)).ToArray())
        {
            board.CustomFields.Remove(removed);
        }
        for (var index = 0; index < catalogue.Count; index++)
        {
            var item = catalogue[index];
            var field = board.CustomFields.SingleOrDefault(field => string.Equals(field.Code, item.Definition.Code, StringComparison.OrdinalIgnoreCase));
            if (field is null)
            {
                field = new EntityCustomField { Board = board, BoardId = board.Id };
                board.CustomFields.Add(field);
            }
            CustomFieldConfigurationCodec.Write(field, item.Definition);
            field.SortOrder = index;
            field.AppliesToAllCardTypes = item.AppliesTo is AllCardTypes;
            var selected = item.AppliesTo is SelectedCardTypes types ? types.CardTypeIds.ToHashSet() : [];
            foreach (var removed in field.CardTypeAssignments.Where(assignment => !selected.Contains(assignment.CardTypeId)).ToArray())
            {
                field.CardTypeAssignments.Remove(removed);
            }
            foreach (var typeId in selected.Except(field.CardTypeAssignments.Select(assignment => assignment.CardTypeId)))
            {
                field.CardTypeAssignments.Add(new EntityCustomFieldCardType { BoardId = board.Id, CustomField = field, CardTypeId = typeId });
            }
        }
    }

    public static IReadOnlyList<CustomFieldDefinition> Resolve(EntityCardType type) => Resolve(Read(type.Board.CustomFields), type.Id);

    public static IReadOnlyList<CustomFieldDefinition> Resolve(IReadOnlyList<CustomFieldDto> fields, int cardTypeId) =>
        fields.Where(field => Applies(field.AppliesTo, cardTypeId)).Select(field => field.Definition).ToArray();

    public static bool Applies(CustomFieldApplicability applicability, int cardTypeId) => applicability switch
    {
        AllCardTypes => true,
        SelectedCardTypes selected => selected.CardTypeIds.Contains(cardTypeId),
        _ => false
    };

    public static CustomFieldRuleResult<IReadOnlyList<CustomFieldDto>> Normalise(
        IReadOnlyList<CustomFieldDto> fields, IReadOnlySet<int> cardTypeIds)
    {
        var definitions = CustomFieldRules.NormaliseDefinitions(fields.Select(field => field.Definition).ToArray());
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

    public static CustomFieldRuleResult<CustomFieldDto> Parse(CustomFieldInput input)
    {
        var parsed = CustomFieldDefinitionInputs.Parse([input.Definition]);
        if (!parsed.IsValid)
        {
            return new(null, parsed.Errors.Select(error => new ValidationError(
                error.Property.Replace("customFields[0]", "definition", StringComparison.Ordinal), error.Message)).ToArray());
        }
        return new(new(parsed.Value![0], input.AppliesTo), []);
    }

    public static IReadOnlyList<CustomFieldDto> RemoveType(IReadOnlyList<CustomFieldDto> fields, int id) =>
        fields.Select(field => field.AppliesTo is SelectedCardTypes selected
            ? field with { AppliesTo = new SelectedCardTypes(selected.CardTypeIds.Where(typeId => typeId != id).ToArray()) }
            : field).ToArray();

    private static string DefinitionPath(string path)
    {
        var end = path.IndexOf(']');
        return end < 0 ? path : path.Insert(end + 1, ".definition");
    }
}
