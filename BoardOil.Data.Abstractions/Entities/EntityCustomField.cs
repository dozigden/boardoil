namespace BoardOil.Data.Abstractions.Entities;

public sealed class EntityCustomField
{
    private string _code = string.Empty;
    private string _name = string.Empty;

    public int Id { get; set; }
    public int BoardId { get; set; }
    public string Code
    {
        get => _code;
        set { _code = value; NormalisedCode = value.ToUpperInvariant(); }
    }
    public string NormalisedCode { get; private set; } = string.Empty;
    public string Name
    {
        get => _name;
        set { _name = value; NormalisedName = value.ToUpperInvariant(); }
    }
    public string NormalisedName { get; private set; } = string.Empty;
    public string? Emoji { get; set; }
    public CustomFieldDataType DataType { get; set; }
    public int SortOrder { get; set; }
    public bool Nullable { get; set; }
    public bool ShowOnCard { get; set; }
    public bool AppliesToAllCardTypes { get; set; }
    public string ConfigurationJson { get; set; } = string.Empty;

    public EntityBoard Board { get; set; } = null!;
    public ICollection<EntityCustomFieldCardType> CardTypeAssignments { get; set; } = new List<EntityCustomFieldCardType>();
}
