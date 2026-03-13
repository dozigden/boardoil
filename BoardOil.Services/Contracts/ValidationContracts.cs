namespace BoardOil.Services.Contracts;

public sealed record ValidationError(
    string Property,
    string Message);
