namespace BoardOil.Data.Abstractions.Entities;

// Temporary packages are tracked before creation, separately from card attachments.
public sealed class EntityTemporaryBoardPackage
{
    public int Id { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string? LastError { get; set; }
}
