using System.Numerics;

namespace BoardOil.Abstractions.Ordering;

public static class SortKeyGenerator
{
    private const int KeyLength = 20;
    private const int BaseValue = 36;
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private static readonly BigInteger MaxValue = BigInteger.Pow(BaseValue, KeyLength) - 1;
    private static readonly BigInteger KeySpaceSize = MaxValue + 1;
    private static readonly BigInteger PreferredRangeStart = KeySpaceSize / 4;
    private static readonly BigInteger PreferredRangeSize = KeySpaceSize / 2;

    public static string Between(string? previous, string? next)
    {
        var low = previous is null ? -1 : Parse(previous);
        var high = next is null ? MaxValue + 1 : Parse(next);

        if (high <= low + 1)
        {
            throw new InvalidOperationException("Unable to allocate a sort key between neighbors.");
        }

        var mid = (low + high) / 2;
        return Format(mid);
    }

    public static IReadOnlyList<string> CreateEvenlySpaced(
        int count,
        IReadOnlyCollection<string>? excludedKeys = null)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (count == 0)
        {
            return [];
        }

        var spacing = PreferredRangeSize / ((BigInteger)count + 1);
        if (spacing <= 0)
        {
            throw new InvalidOperationException("Unable to allocate the requested number of sort keys.");
        }

        var forbiddenOffsets = ResolveForbiddenOffsets(count, spacing, excludedKeys);
        BigInteger offset = 0;
        while (forbiddenOffsets.Contains(offset))
        {
            offset++;
        }

        if (offset >= spacing)
        {
            throw new InvalidOperationException("Unable to allocate sort keys outside the excluded key set.");
        }

        var keys = new List<string>(count);
        for (var index = 0; index < count; index++)
        {
            var value = PreferredRangeStart + (spacing * (index + 1)) + offset;
            keys.Add(Format(value));
        }

        return keys;
    }

    private static HashSet<BigInteger> ResolveForbiddenOffsets(
        int count,
        BigInteger spacing,
        IReadOnlyCollection<string>? excludedKeys)
    {
        var forbiddenOffsets = new HashSet<BigInteger>();
        if (excludedKeys is null)
        {
            return forbiddenOffsets;
        }

        foreach (var excludedKey in excludedKeys)
        {
            BigInteger value;
            try
            {
                value = Parse(excludedKey);
            }
            catch (ArgumentException)
            {
                continue;
            }

            var valueInPreferredRange = value - PreferredRangeStart;
            if (valueInPreferredRange < 0)
            {
                continue;
            }

            var sequenceIndex = BigInteger.DivRem(valueInPreferredRange, spacing, out var offset);
            if (sequenceIndex >= 1 && sequenceIndex <= count)
            {
                forbiddenOffsets.Add(offset);
            }
        }

        return forbiddenOffsets;
    }

    private static BigInteger Parse(string key)
    {
        if (key.Length != KeyLength)
        {
            throw new ArgumentException($"Sort key must be exactly {KeyLength} characters.", nameof(key));
        }

        BigInteger value = 0;
        foreach (var raw in key)
        {
            var c = char.ToUpperInvariant(raw);
            var digit = Alphabet.IndexOf(c);
            if (digit < 0)
            {
                throw new ArgumentException("Sort key contains invalid characters.", nameof(key));
            }

            value = (value * BaseValue) + digit;
        }

        return value;
    }

    private static string Format(BigInteger value)
    {
        if (value < 0 || value > MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        var chars = new char[KeyLength];
        var remainder = value;

        for (var i = KeyLength - 1; i >= 0; i--)
        {
            remainder = BigInteger.DivRem(remainder, BaseValue, out var digit);
            chars[i] = Alphabet[(int)digit];
        }

        return new string(chars);
    }
}
