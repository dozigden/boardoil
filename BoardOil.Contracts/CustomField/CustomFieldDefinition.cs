using System.Text.Json.Serialization;

namespace BoardOil.Contracts.CustomField;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "dataType")]
[JsonDerivedType(typeof(TextCustomFieldDefinition), "text")]
[JsonDerivedType(typeof(IntegerCustomFieldDefinition), "integer")]
[JsonDerivedType(typeof(DateCustomFieldDefinition), "date")]
public abstract record CustomFieldDefinition
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? Emoji { get; init; }
    public bool Nullable { get; init; }
    public bool ShowOnCard { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TextCustomFieldDefinition : CustomFieldDefinition
{
    public string? DefaultValue { get; init; }
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record IntegerCustomFieldDefinition : CustomFieldDefinition
{
    public int? DefaultValue { get; init; }
    public int? Min { get; init; }
    public int? Max { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DateCustomFieldDefinition : CustomFieldDefinition
{
    public DateOnly? DefaultValue { get; init; }
}
