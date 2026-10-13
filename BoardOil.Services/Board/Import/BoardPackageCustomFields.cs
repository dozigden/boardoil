using BoardOil.Contracts.Board;
using BoardOil.Contracts.CustomField;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.CustomField;

namespace BoardOil.Services.Board.Import;

internal static class BoardPackageCustomFields
{
    public static IReadOnlyList<BoardPackageCustomField> Export(IEnumerable<EntityCustomField> fields, IReadOnlyList<EntityCardType> types)
    {
        var names = types.ToDictionary(type => type.Id, type => type.Name);
        return CustomFieldCatalogue.Read(fields).Select(field => new BoardPackageCustomField(field.Definition,
            field.AppliesTo is SelectedCardTypes selected
                ? new("selected", selected.CardTypeIds.Select(id => names[id]).ToArray())
                : new("all"))).ToArray();
    }

    public static CustomFieldRuleResult<IReadOnlyList<BoardPackageCustomField>> Normalise(
        IReadOnlyList<BoardPackageCustomField>? fields, IReadOnlyDictionary<string, string> typeNames)
    {
        if (fields is null || fields.Any(field => field is null))
        {
            return new(null, [new("board.customFields", "Field definitions must be an array of objects")]);
        }
        var definitions = CustomFieldRules.NormaliseDefinitions(fields.Select(field => field.Definition).ToArray());
        var errors = new List<ValidationError>(definitions.Errors.Select(error =>
            new ValidationError(DefinitionPath(error.Property), error.Message)));
        var result = new List<BoardPackageCustomField>();
        for (var index = 0; index < fields.Count; index++)
        {
            var field = fields[index];
            var applies = field.AppliesTo;
            var path = $"board.customFields[{index}].appliesTo";
            if (applies is null || (applies.Mode != "all" && applies.Mode != "selected"))
            {
                errors.Add(new(path, "Supported applicability required"));
                continue;
            }
            if (applies.Mode == "all" && applies.CardTypeNames is not null)
            {
                errors.Add(new(path, "All types must not specify selected names"));
            }
            if (applies.Mode == "selected")
            {
                var names = applies.CardTypeNames;
                if (names is null || names.Any(name => name is null || !typeNames.ContainsKey(name.Trim())))
                {
                    errors.Add(new(path, "Card type does not exist in board"));
                    continue;
                }
                var canonical = names.Select(name => typeNames[name.Trim()]).ToArray();
                if (canonical.Distinct(StringComparer.OrdinalIgnoreCase).Count() != canonical.Length)
                {
                    errors.Add(new(path, "Repeated card type"));
                }
                applies = new("selected", canonical);
            }
            if (definitions.IsValid) { result.Add(new(definitions.Value![index], applies)); }
        }
        return errors.Count == 0 ? new(result, []) : new(null, errors);
    }

    private static string DefinitionPath(string path)
    {
        var end = path.IndexOf(']');
        if (end >= 0) { path = path.Insert(end + 1, ".definition"); }
        return $"board.{path}";
    }

    public static IReadOnlyList<CustomFieldDefinition> Resolve(IReadOnlyList<BoardPackageCustomField> fields, string typeName) =>
        fields.Where(field => field.AppliesTo.Mode == "all"
            || field.AppliesTo.CardTypeNames!.Contains(typeName, StringComparer.OrdinalIgnoreCase))
            .Select(field => field.Definition).ToArray();

    public static void Persist(EntityBoard board, IReadOnlyList<BoardPackageCustomField> fields, IReadOnlyDictionary<string, EntityCardType> types) =>
        CustomFieldCatalogue.Apply(board, fields.Select(field => new CustomFieldDto(field.Definition,
            field.AppliesTo.Mode == "all" ? new AllCardTypes()
                : new SelectedCardTypes(field.AppliesTo.CardTypeNames!.Select(name => types[name.ToUpperInvariant()].Id).ToArray()))).ToArray());
}
