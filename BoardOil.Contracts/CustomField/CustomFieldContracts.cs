using System.Text.Json.Serialization;

namespace BoardOil.Contracts.CustomField;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "mode")]
[JsonDerivedType(typeof(AllCardTypes), "all")]
[JsonDerivedType(typeof(SelectedCardTypes), "selected")]
public abstract record CustomFieldApplicability;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AllCardTypes : CustomFieldApplicability;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SelectedCardTypes(IReadOnlyList<int> CardTypeIds) : CustomFieldApplicability;

public sealed record CustomFieldDto(CustomFieldDefinition Definition, CustomFieldApplicability AppliesTo);
public sealed record CustomFieldInput(CustomFieldDefinitionInput Definition, CustomFieldApplicability AppliesTo);
public sealed record CustomFieldOrderRequest(IReadOnlyList<string> Codes);
public sealed record CustomFieldPreviewRequest(CustomFieldInput? Field);
public sealed record CustomFieldPreviewContract(CustomFieldDto? Field);
public sealed record CustomFieldPreviewDto(int AffectedCardCount, int RemovedValueCount, int ReplacedValueCount);
