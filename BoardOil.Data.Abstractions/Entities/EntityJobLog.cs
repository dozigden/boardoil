namespace BoardOil.Data.Abstractions.Entities;

public sealed class EntityJobLog : ISupportCreatedAt, ISupportUpdatedAt
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public EntityJob Job { get; set; } = null!;
    public JobLogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? DataJson { get; set; }
    public DateTime LoggedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
