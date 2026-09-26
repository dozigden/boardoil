namespace BoardOil.Data.Abstractions.Entities;

public sealed class EntityJob : ISupportCreatedAt, ISupportUpdatedAt
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public JobStatus Status { get; set; }
    public DateTime RunAfterUtc { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string ResultJson { get; set; } = "{}";
    public string? ErrorMessage { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int? UserId { get; set; }
    public string? CorrelationId { get; set; }
    public List<EntityJobLog> Logs { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
