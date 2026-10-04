using System.Text.Json.Serialization;

namespace BoardOil.Contracts.CardField;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextFieldDefinition), "text")]
[JsonDerivedType(typeof(IntegerFieldDefinition), "integer")]
[JsonDerivedType(typeof(DateFieldDefinition), "date")]
public abstract record CardFieldDefinition
{
    public required string FieldCode { get; init; }
    public required string DisplayName { get; init; }
    public string? Emoji { get; init; }
    public bool Nullable { get; init; }
    public bool ShowOnCard { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TextFieldDefinition : CardFieldDefinition
{
    public string? DefaultValue { get; init; }
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record IntegerFieldDefinition : CardFieldDefinition
{
    public int? DefaultValue { get; init; }
    public int? Min { get; init; }
    public int? Max { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DateFieldDefinition : CardFieldDefinition
{
    public DateOnly? DefaultValue { get; init; }
}
