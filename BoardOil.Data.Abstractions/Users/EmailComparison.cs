namespace BoardOil.Data.Abstractions.Users;

public static class EmailComparison
{
    public static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    public static int Compare(string? left, string? right) =>
        string.CompareOrdinal(Normalise(left), Normalise(right));
}
