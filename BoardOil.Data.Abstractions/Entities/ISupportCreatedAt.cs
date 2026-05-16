namespace BoardOil.Data.Abstractions.Entities;

public interface ISupportCreatedAt
{
    DateTime CreatedAtUtc { get; set; }
}
