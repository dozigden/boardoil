using BoardOil.Services.Contracts;
using System.Text.RegularExpressions;

namespace BoardOil.Services.Card;

public sealed class CardValidator : ICardValidator
{
    private static readonly Regex AllowedCardTitleRegex =
        new("^[A-Za-z0-9][A-Za-z0-9 \\-._&'(),!?:/]*$", RegexOptions.Compiled);

    public IReadOnlyList<ValidationError> ValidateCreate(CreateCardRequest request)
    {
        var errors = new List<ValidationError>();
        ValidateTitle(request.Title, errors);
        ValidateDescription(request.Description, errors);
        return errors;
    }

    public IReadOnlyList<ValidationError> ValidateUpdate(UpdateCardRequest request)
    {
        var errors = new List<ValidationError>();
        if (request.Title is not null)
        {
            ValidateTitle(request.Title, errors);
        }

        if (request.Description is not null)
        {
            ValidateDescription(request.Description, errors);
        }

        return errors;
    }

    private static void ValidateTitle(string title, ICollection<ValidationError> errors)
    {
        var normalized = title.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            errors.Add(new ValidationError("title", "Card title is required."));
            return;
        }

        if (normalized.Length > 200)
        {
            errors.Add(new ValidationError("title", "Card title must be 200 characters or fewer."));
            return;
        }

        if (!AllowedCardTitleRegex.IsMatch(normalized))
        {
            errors.Add(new ValidationError("title", "Card title can only contain letters, numbers, spaces, and . , - _ & ' ( ) ! ? : /"));
        }
    }

    private static void ValidateDescription(string description, ICollection<ValidationError> errors)
    {
        if (description.Length > 5000)
        {
            errors.Add(new ValidationError("description", "Card description must be 5000 characters or fewer."));
        }
    }
}
