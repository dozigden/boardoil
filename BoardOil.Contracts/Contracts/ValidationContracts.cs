namespace BoardOil.Contracts.Contracts;

public sealed record ValidationError(
    string Property,
    string Message);
