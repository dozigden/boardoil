using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Data.Abstractions.Card;
using BoardOil.Services.Slick;
namespace BoardOil.Services.Card;

public sealed class CardValidator(
    ICardRepository cardRepository,
    IBoardMemberRepository boardMemberRepository) : ICardValidator
{
    private const int MaxDescriptionLength = 20_000;
    private const int MaxTagNameLength = 40;
    private readonly ICardRepository _cardRepository = cardRepository;
    private readonly IBoardMemberRepository _boardMemberRepository = boardMemberRepository;

    public async Task<IReadOnlyList<ValidationError>> ValidateCreateAsync(int boardId, CreateCardRequest request)
    {
        var errors = new List<ValidationError>();
        ValidateTitle(request.Title, errors);
        ValidateDescription(request.Description ?? string.Empty, errors);
        await ValidateAssignedUserIdAsync(boardId, request.AssignedUserId, errors);
        var createSlickValidationError = SlickNameValidation.ValidateOptional(request.SlickName, "slickName");
        if (createSlickValidationError is not null)
        {
            errors.Add(createSlickValidationError);
        }
        var createExternalUrlValidationError = CardExternalUrl.ValidateOptional(request.ExternalUrl, "externalUrl");
        if (createExternalUrlValidationError is not null)
        {
            errors.Add(createExternalUrlValidationError);
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        if (request.BoardColumnId is int boardColumnId)
        {
            var columnExists = await _cardRepository.ColumnExistsAsync(boardColumnId);
            if (!columnExists)
            {
                errors.Add(new ValidationError("boardColumnId", "Column does not exist."));
                return errors;
            }
        }

        var tagValidationErrors = ValidateTagNames(request.TagNames);
        return tagValidationErrors;
    }

    public async Task<IReadOnlyList<ValidationError>> ValidateUpdateAsync(int boardId, UpdateCardRequest request)
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

        if (request.CardTypeId <= 0)
        {
            errors.Add(new ValidationError("cardTypeId", "Card type is required."));
        }

        await ValidateAssignedUserIdAsync(boardId, request.AssignedUserId, errors);
        var updateSlickValidationError = SlickNameValidation.ValidateOptional(request.SlickName, "slickName");
        if (updateSlickValidationError is not null)
        {
            errors.Add(updateSlickValidationError);
        }
        var updateExternalUrlValidationError = CardExternalUrl.ValidateOptional(request.ExternalUrl, "externalUrl");
        if (updateExternalUrlValidationError is not null)
        {
            errors.Add(updateExternalUrlValidationError);
        }

        if (request.BoardColumnId is int boardColumnId)
        {
            if (boardColumnId <= 0)
            {
                errors.Add(new ValidationError("boardColumnId", "Column is required."));
            }
            else
            {
                var columnExists = await _cardRepository.ColumnExistsAsync(boardColumnId);
                if (!columnExists)
                {
                    errors.Add(new ValidationError("boardColumnId", "Column does not exist."));
                }
            }
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        var tagValidationErrors = ValidateTagNames(request.TagNames!);
        if (tagValidationErrors.Count > 0)
        {
            return tagValidationErrors;
        }

        return Array.Empty<ValidationError>();
    }

    private async Task ValidateAssignedUserIdAsync(int boardId, int? assignedUserId, ICollection<ValidationError> errors)
    {
        if (assignedUserId is null)
        {
            return;
        }

        if (assignedUserId.Value <= 0)
        {
            errors.Add(new ValidationError("assignedUserId", "Assigned user is invalid."));
            return;
        }

        var membership = await _boardMemberRepository.GetByBoardAndUserAsync(boardId, assignedUserId.Value);
        if (membership is null || !membership.User.IsActive)
        {
            errors.Add(new ValidationError("assignedUserId", "Assigned user must be an active board member."));
        }
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

        if (ContainsControlCharacters(normalized))
        {
            errors.Add(new ValidationError("title", "Card title cannot contain control characters."));
        }
    }

    private static void ValidateDescription(string description, ICollection<ValidationError> errors)
    {
        if (description.Length > MaxDescriptionLength)
        {
            errors.Add(new ValidationError("description", $"Card description must be {MaxDescriptionLength} characters or fewer."));
        }
    }

    private static IReadOnlyList<ValidationError> ValidateTagNames(IReadOnlyList<string>? tagNames)
    {
        if (tagNames is null || tagNames.Count == 0)
        {
            return Array.Empty<ValidationError>();
        }

        var tagValidationErrors = new List<ValidationError>();
        foreach (var tagName in tagNames)
        {
            var canonicalName = tagName.Trim();
            if (string.IsNullOrWhiteSpace(canonicalName))
            {
                continue;
            }

            if (canonicalName.Length > MaxTagNameLength)
            {
                tagValidationErrors.Add(new ValidationError("tagNames", $"Tag '{canonicalName}' must be {MaxTagNameLength} characters or fewer."));
            }
        }

        return tagValidationErrors.Count == 0 ? Array.Empty<ValidationError>() : tagValidationErrors;
    }

    private static bool ContainsControlCharacters(string value)
    {
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }
}
