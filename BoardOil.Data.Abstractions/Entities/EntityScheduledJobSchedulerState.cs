namespace BoardOil.Data.Abstractions.Entities;

public sealed class EntityScheduledJobSchedulerState : ISupportCreatedAt, ISupportUpdatedAt
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    // The latest schedule evaluation baseline, regardless of whether a job succeeded.
    public DateTime LastRunTimeUtc { get; set; }
    public DateTime? LastEvaluatedAtUtc { get; set; }
    // A migration-requested Once pass remains requested until all target jobs are queued.
    public bool RunRequested { get; set; }
    public DateTime? PendingDueAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
