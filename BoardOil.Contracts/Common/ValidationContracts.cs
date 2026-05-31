namespace BoardOil.Contracts.Common;

public sealed record ValidationError(
    string Property,
    string Message);
