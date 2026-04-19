namespace BoardOil.Persistence.Abstractions.Entities;

public sealed class EntityArchivedCard
{
    public int Id { get; set; }
    public int BoardId { get; set; }
    public int OriginalCardId { get; set; }
    public DateTime ArchivedAtUtc { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
    public string SearchTitle { get; set; } = string.Empty;
    public string SearchTagsJson { get; set; } = string.Empty;
    public string SearchTextNormalised { get; set; } = string.Empty;

    public EntityBoard Board { get; set; } = null!;
}
