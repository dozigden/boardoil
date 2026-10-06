using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoardOil.Contracts.CardField;

// Request scalars retain their JSON kinds until validation can report field-specific errors.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextFieldDefinitionInput), "text")]
[JsonDerivedType(typeof(IntegerFieldDefinitionInput), "integer")]
[JsonDerivedType(typeof(DateFieldDefinitionInput), "date")]
public abstract record CardFieldDefinitionInput
{
    public required string FieldCode { get; init; }
    public required string DisplayName { get; init; }
    public string? Emoji { get; init; }
    public bool Nullable { get; init; }
    public bool ShowOnCard { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TextFieldDefinitionInput : CardFieldDefinitionInput
{
    public string? DefaultValue { get; init; }
    public JsonElement? MinLength { get; init; }
    public JsonElement? MaxLength { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record IntegerFieldDefinitionInput : CardFieldDefinitionInput
{
    public JsonElement? DefaultValue { get; init; }
    public JsonElement? Min { get; init; }
    public JsonElement? Max { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DateFieldDefinitionInput : CardFieldDefinitionInput
{
    public JsonElement? DefaultValue { get; init; }
}
