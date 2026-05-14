namespace BoardOil.Persistence.Abstractions.Entities;

public interface ISupportCreatedAt
{
    DateTime CreatedAtUtc { get; set; }
}
