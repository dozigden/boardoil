using System.Numerics;

namespace BoardOil.Abstractions.Ordering;

public static class SortKeyGenerator
{
    private const int KeyLength = 20;
    private const int BaseValue = 36;
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private static readonly BigInteger MaxValue = BigInteger.Pow(BaseValue, KeyLength) - 1;

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
