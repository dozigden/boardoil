namespace BoardOil.Data.Abstractions.Entities;

public sealed class EntityAppSetting : ISupportUpdatedAt
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; internal set; }
}
