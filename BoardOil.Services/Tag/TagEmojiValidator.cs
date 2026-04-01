using BoardOil.Contracts.Contracts;
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

        if (!LooksLikeEmoji(canonicalEmoji))
        {
            return new EmojiValidationResult(
                null,
                new ValidationError(propertyName, "Emoji must be a single valid emoji."));
        }

        return new EmojiValidationResult(canonicalEmoji, null);
    }

    private static bool LooksLikeEmoji(string value)
    {
        var hasEmojiBase = false;
        var hasKeycapBase = false;
        var hasKeycapCombiner = false;

        foreach (var rune in value.EnumerateRunes())
        {
            var codePoint = rune.Value;

            if (IsEmojiBase(codePoint))
            {
                hasEmojiBase = true;
                continue;
            }

            if (IsKeycapBase(codePoint))
            {
                hasKeycapBase = true;
                continue;
            }

            if (codePoint == 0x20E3) // Combining enclosing keycap
            {
                hasKeycapCombiner = true;
                continue;
            }

            if (IsEmojiJoinerOrModifier(codePoint))
            {
                continue;
            }

            return false;
        }

        return hasEmojiBase || (hasKeycapBase && hasKeycapCombiner);
    }

    private static bool IsEmojiBase(int codePoint) =>
        codePoint switch
        {
            0x00A9 or // Copyright
            0x00AE or // Registered
            0x203C or
            0x2049 or
            0x2122 or
            0x2139 or
            0x24C2 or
            0x3030 or
            0x303D or
            0x3297 or
            0x3299 => true,
            _ => IsInRange(codePoint, 0x1F1E6, 0x1F1FF) // Regional indicators
                || IsInRange(codePoint, 0x1F300, 0x1FAFF) // Main emoji planes
                || IsInRange(codePoint, 0x2194, 0x21AA)
                || IsInRange(codePoint, 0x2300, 0x23FF)
                || IsInRange(codePoint, 0x25AA, 0x25FF)
                || IsInRange(codePoint, 0x2600, 0x27BF)
        };

    private static bool IsKeycapBase(int codePoint) =>
        IsInRange(codePoint, '0', '9') || codePoint is '#' or '*';

    private static bool IsEmojiJoinerOrModifier(int codePoint) =>
        codePoint is
            0x200D or // ZWJ
            0xFE0E or // Text presentation selector
            0xFE0F // Emoji presentation selector
            || IsInRange(codePoint, 0x1F3FB, 0x1F3FF) // Fitzpatrick modifiers
            || IsInRange(codePoint, 0xE0020, 0xE007F); // Tag sequence chars

    private static bool IsInRange(int value, int min, int max) =>
        value >= min && value <= max;

    public sealed record EmojiValidationResult(
        string? CanonicalEmoji,
        ValidationError? Error);
}
