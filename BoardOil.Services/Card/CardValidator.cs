using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Contracts;
using BoardOil.Persistence.Abstractions.Card;
using BoardOil.Persistence.Abstractions.Tag;
using System.Text.RegularExpressions;

namespace BoardOil.Services.Card;

public sealed class CardValidator(
    ICardRepository cardRepository,
    ITagRepository tagRepository) : ICardValidator
{
    private readonly ICardRepository _cardRepository = cardRepository;
    private readonly ITagRepository _tagRepository = tagRepository;

    private static readonly Regex AllowedCardTitleRegex =
        new("^[A-Za-z0-9][A-Za-z0-9 \\-._&'(),!?:/]*$", RegexOptions.Compiled);

    public async Task<IReadOnlyList<ValidationError>> ValidateCreateAsync(CreateCardRequest request)
    {
        var errors = new List<ValidationError>();
        ValidateTitle(request.Title, errors);
        ValidateDescription(request.Description, errors);
        if (errors.Count > 0)
        {
            return errors;
        }

        var columnExists = await _cardRepository.ColumnExistsAsync(request.BoardColumnId);
        if (!columnExists)
        {
            errors.Add(new ValidationError("boardColumnId", "Column does not exist."));
            return errors;
        }

        var tagValidationErrors = await ValidateTagNamesExistAsync(request.TagNames);
        return tagValidationErrors;
    }

    public async Task<IReadOnlyList<ValidationError>> ValidateUpdateAsync(UpdateCardRequest request)
    {
        var errors = new List<ValidationError>();
        if (request.Title.IsTrimmedNullOrEmpty())
        {
            errors.Add(new ValidationError("title", "Card title is required."));
        }
        else
        {
            ValidateTitle(request.Title, errors);
        }

        if (request.Description is null)
        {
            errors.Add(new ValidationError("description", "Card description is required."));
        }
        else
        {
            ValidateDescription(request.Description, errors);
        }

        if (request.TagNames is null)
        {
            errors.Add(new ValidationError("tagNames", "Tag names are required."));
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        var tagValidationErrors = await ValidateTagNamesExistAsync(request.TagNames!);
        if (tagValidationErrors.Count > 0)
        {
            return tagValidationErrors;
        }

        return Array.Empty<ValidationError>();
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

    private async Task<IReadOnlyList<ValidationError>> ValidateTagNamesExistAsync(IReadOnlyList<string>? tagNames)
    {
        if (tagNames is null || tagNames.Count == 0)
        {
            return Array.Empty<ValidationError>();
        }

        var missingTagErrors = new List<ValidationError>();
        foreach (var tagName in tagNames)
        {
            var existingTag = await _tagRepository.GetByNormalisedNameAsync(tagName.ToUpperInvariant());
            if (existingTag is null)
            {
                missingTagErrors.Add(new ValidationError("tagNames", $"Tag '{tagName}' does not exist."));
            }
        }

        return missingTagErrors.Count == 0 ? Array.Empty<ValidationError>() : missingTagErrors;
    }
}
