namespace BoardOil.Persistence.Abstractions.Entities;

public interface ISupportUpdatedAt
{
    DateTime UpdatedAtUtc { get; }
}
