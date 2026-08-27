using BoardOil.Contracts.Common;
using System.Globalization;

namespace BoardOil.Services.Tag;

public static class TagEmojiValidator
{
    private const int MaxEmojiStorageLength = 32;

    public static EmojiValidationResult ValidateAndNormalise(string? rawEmoji, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(rawEmoji))
        {
            return new EmojiValidationResult(null, null);
        }

        var canonicalEmoji = rawEmoji.Trim();
        if (canonicalEmoji.Length > MaxEmojiStorageLength)
        {
            return new EmojiValidationResult(
                null,
                new ValidationError(propertyName, "Emoji must be a single valid emoji."));
        }

        if (StringInfo.ParseCombiningCharacters(canonicalEmoji).Length != 1)
        {
            return new EmojiValidationResult(
                null,
                new ValidationError(propertyName, "Emoji must be a single valid emoji."));
        }

        return new EmojiValidationResult(canonicalEmoji, null);
    }

    public sealed record EmojiValidationResult(
        string? CanonicalEmoji,
        ValidationError? Error);
}
