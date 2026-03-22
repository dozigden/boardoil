namespace BoardOil.Services.Card;

public static class StringExtensions
{
    public static bool IsTrimmedNullOrEmpty(this string? value) =>
        string.IsNullOrEmpty(value?.Trim());
}
