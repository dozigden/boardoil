using System.Text.RegularExpressions;

namespace BoardOil.Services.CardField;

// Temporary history destination until the system-comment integration slice.
internal static class CardFieldLossDescription
{
    private static readonly Regex MarkdownPunctuation = new(@"([\\`*_{}\[\]()#+\-.!|>~<])", RegexOptions.CultureInvariant);

    public static string Append(string description, IReadOnlyList<LostCardFieldValue> losses) =>
        description + "\n\nFields removed during card type change:\n\n"
        + string.Join("\n", losses.Select(loss => $"- {Escape(loss.DisplayName)}: {Escape(loss.Value)}"));

    private static string Escape(string value) => MarkdownPunctuation.Replace(value, @"\$1");
}
