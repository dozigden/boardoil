using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoardOil.Contracts.CustomField;

// Request scalars retain their JSON kinds until validation can report field-specific errors.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "dataType")]
[JsonDerivedType(typeof(TextCustomFieldDefinitionInput), "text")]
[JsonDerivedType(typeof(IntegerCustomFieldDefinitionInput), "integer")]
[JsonDerivedType(typeof(DateCustomFieldDefinitionInput), "date")]
public abstract record CustomFieldDefinitionInput
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? Emoji { get; init; }
    public bool Nullable { get; init; }
    public bool ShowOnCard { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TextCustomFieldDefinitionInput : CustomFieldDefinitionInput
{
    public string? DefaultValue { get; init; }
    public JsonElement? MinLength { get; init; }
    public JsonElement? MaxLength { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record IntegerCustomFieldDefinitionInput : CustomFieldDefinitionInput
{
    public JsonElement? DefaultValue { get; init; }
    public JsonElement? Min { get; init; }
    public JsonElement? Max { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DateCustomFieldDefinitionInput : CustomFieldDefinitionInput
{
    public JsonElement? DefaultValue { get; init; }
}
