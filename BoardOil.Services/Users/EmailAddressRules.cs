using BoardOil.Contracts.Contracts;

namespace BoardOil.Services.Users;

internal static class EmailAddressRules
{
    private const int MaxEmailLength = 320;

    public static string? TryNormalise(string emailValue) =>
        string.IsNullOrWhiteSpace(emailValue) ? null : emailValue.Trim().ToLowerInvariant();

    public static IReadOnlyList<ValidationError> Validate(string? emailValue, string fieldName)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(emailValue))
        {
            errors.Add(new ValidationError(fieldName, "Email is required."));
            return errors;
        }

        var trimmed = emailValue.Trim();
        if (trimmed.Length > MaxEmailLength)
        {
            errors.Add(new ValidationError(fieldName, $"Email must be {MaxEmailLength} characters or fewer."));
            return errors;
        }

        var atIndex = trimmed.IndexOf('@');
        if (atIndex <= 0 || atIndex != trimmed.LastIndexOf('@') || atIndex >= trimmed.Length - 1)
        {
            errors.Add(new ValidationError(fieldName, "Email must contain '@' with characters before and after it."));
        }

        return errors;
    }
}
