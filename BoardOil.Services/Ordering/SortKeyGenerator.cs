namespace BoardOil.Services.Ordering;

internal static class SortKeyGenerator
{
    public static string Between(string? previous, string? next) =>
        BoardOil.Abstractions.Ordering.SortKeyGenerator.Between(previous, next);

    public static IReadOnlyList<string> CreateEvenlySpaced(
        int count,
        IReadOnlyCollection<string>? excludedKeys = null) =>
        BoardOil.Abstractions.Ordering.SortKeyGenerator.CreateEvenlySpaced(count, excludedKeys);
}
