using BoardOil.Contracts.Contracts;

namespace BoardOil.Services.Slick;

internal static class SlickNameValidation
{
    public const int MaxSlickNameLength = 40;

    public static (string CanonicalName, string NormalisedName, ValidationError? Error) ValidateRequired(string? rawName, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return (string.Empty, string.Empty, new ValidationError(propertyName, "Slick name is required."));
        }

        var canonicalName = rawName.Trim();
        if (canonicalName.Length > MaxSlickNameLength)
        {
            return (
                string.Empty,
                string.Empty,
                new ValidationError(propertyName, $"Slick '{canonicalName}' must be {MaxSlickNameLength} characters or fewer."));
        }

        return (canonicalName, canonicalName.ToUpperInvariant(), null);
    }

    public static ValidationError? ValidateOptional(string? rawName, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return null;
        }

        var canonicalName = rawName.Trim();
        if (canonicalName.Length > MaxSlickNameLength)
        {
            return new ValidationError(propertyName, $"Slick '{canonicalName}' must be {MaxSlickNameLength} characters or fewer.");
        }

        return null;
    }
}
