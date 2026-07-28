using BoardOil.Contracts.Common;

namespace BoardOil.Services.Card;

public static class CardExternalUrl
{
    public static string? Normalise(string? externalUrl)
    {
        if (string.IsNullOrWhiteSpace(externalUrl))
        {
            return null;
        }

        return externalUrl.Trim();
    }

    public static ValidationError? ValidateOptional(string? externalUrl, string fieldName)
    {
        var normalisedExternalUrl = Normalise(externalUrl);
        if (normalisedExternalUrl is null)
        {
            return null;
        }

        if (!Uri.TryCreate(normalisedExternalUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            return new ValidationError(fieldName, "External URL must be an absolute HTTP or HTTPS URL.");
        }

        return null;
    }
}
