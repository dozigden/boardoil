namespace BoardOil.Data.Abstractions.Entities;

public sealed class EntityScheduledJobSchedulerState : ISupportCreatedAt, ISupportUpdatedAt
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    // The latest schedule evaluation baseline, regardless of whether a job succeeded.
    public DateTime LastRunTimeUtc { get; set; }
    public DateTime? LastEvaluatedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
