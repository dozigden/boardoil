using BoardOil.Services.Contracts;
using System.Text.RegularExpressions;

namespace BoardOil.Services.Column;

public sealed class ColumnValidator : IColumnValidator
{
    private static readonly Regex AllowedColumnTitleRegex =
        new("^[A-Za-z0-9][A-Za-z0-9 \\-._&'(),!?:/]*$", RegexOptions.Compiled);

    public IReadOnlyList<ValidationError> ValidateCreate(CreateColumnRequest request)
    {
        var errors = new List<ValidationError>();
        ValidateTitle(request.Title, errors);
        return errors;
    }

    public IReadOnlyList<ValidationError> ValidateUpdate(UpdateColumnRequest request)
    {
        var errors = new List<ValidationError>();
        if (request.Title is not null)
        {
            ValidateTitle(request.Title, errors);
        }

        return errors;
    }

    private static void ValidateTitle(string title, ICollection<ValidationError> errors)
    {
        var normalized = title.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            errors.Add(new ValidationError("title", "Column title is required."));
            return;
        }

        if (normalized.Length > 200)
        {
            errors.Add(new ValidationError("title", "Column title must be 200 characters or fewer."));
            return;
        }

        if (!AllowedColumnTitleRegex.IsMatch(normalized))
        {
            errors.Add(new ValidationError("title", "Column title can only contain letters, numbers, spaces, and . , - _ & ' ( ) ! ? : /"));
        }
    }
}
