namespace BoardOil.Persistence.Abstractions.Entities;

public sealed class EntityAppSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}
