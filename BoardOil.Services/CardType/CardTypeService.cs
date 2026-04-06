using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.CardType;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.CardType;
using BoardOil.Contracts.Contracts;
using BoardOil.Persistence.Abstractions.Board;
using BoardOil.Persistence.Abstractions.Card;
using BoardOil.Persistence.Abstractions.CardType;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Services.Tag;

namespace BoardOil.Services.CardType;

public sealed class CardTypeService(
    IBoardRepository boardRepository,
    ICardTypeRepository cardTypeRepository,
    ICardRepository cardRepository,
    IBoardAuthorisationService boardAuthorisationService,
    IBoardEvents boardEvents,
    IDbContextScopeFactory scopeFactory) : ICardTypeService
{
    private const int MaxCardTypeNameLength = 40;
    private readonly IDbContextScopeFactory _scopeFactory = scopeFactory;
    private readonly IBoardEvents _boardEvents = boardEvents;

    public async Task<ApiResult<IReadOnlyList<CardTypeDto>>> GetCardTypesAsync(int boardId, int actorUserId)
    {
        using var scope = _scopeFactory.CreateReadOnly();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardAccess);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have access to this board.");
        }

        var cardTypes = await cardTypeRepository.GetAllForBoardAsync(boardId);
        return cardTypes.Select(x => x.ToCardTypeDto()).ToList();
    }

    public async Task<ApiResult<CardTypeDto>> CreateCardTypeAsync(int boardId, CreateCardTypeRequest request, int actorUserId)
    {
        using var scope = _scopeFactory.Create();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardManageSettings);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var nameValidation = ValidateCardTypeName(request.Name, "name");
        var emojiValidation = TagEmojiValidator.ValidateAndNormalise(request.Emoji, "emoji");
        var validationErrors = new List<ValidationError>();
        if (nameValidation.Error is not null)
        {
            validationErrors.Add(nameValidation.Error);
        }

        if (emojiValidation.Error is not null)
        {
            validationErrors.Add(emojiValidation.Error);
        }

        if (nameValidation.Error is null)
        {
            var existing = await cardTypeRepository.GetByNormalisedNameAsync(boardId, nameValidation.NormalisedName);
            if (existing is not null)
            {
                validationErrors.Add(new ValidationError("name", $"Card type '{nameValidation.CanonicalName}' already exists."));
            }
        }

        if (validationErrors.Count > 0)
        {
            return ValidationFail(validationErrors);
        }

        var now = DateTime.UtcNow;
        var entity = new EntityCardType
        {
            BoardId = boardId,
            Name = nameValidation.CanonicalName,
            Emoji = emojiValidation.CanonicalEmoji,
            IsSystem = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        cardTypeRepository.Add(entity);

        await scope.SaveChangesAsync();
        await _boardEvents.ResyncRequestedAsync(boardId);

        return ApiResults.Created(entity.ToCardTypeDto());
    }

    public async Task<ApiResult<CardTypeDto>> UpdateCardTypeAsync(int boardId, int cardTypeId, UpdateCardTypeRequest request, int actorUserId)
    {
        using var scope = _scopeFactory.Create();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardManageSettings);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var existing = await cardTypeRepository.GetByIdInBoardAsync(boardId, cardTypeId);
        if (existing is null)
        {
            return ApiErrors.NotFound("Card type not found.");
        }

        var nameValidation = ValidateCardTypeName(request.Name, "name");
        var emojiValidation = TagEmojiValidator.ValidateAndNormalise(request.Emoji, "emoji");
        var validationErrors = new List<ValidationError>();
        if (nameValidation.Error is not null)
        {
            validationErrors.Add(nameValidation.Error);
        }

        if (emojiValidation.Error is not null)
        {
            validationErrors.Add(emojiValidation.Error);
        }

        if (nameValidation.Error is null)
        {
            var byName = await cardTypeRepository.GetByNormalisedNameAsync(boardId, nameValidation.NormalisedName);
            if (byName is not null && byName.Id != existing.Id)
            {
                validationErrors.Add(new ValidationError("name", $"Card type '{nameValidation.CanonicalName}' already exists."));
            }
        }

        if (validationErrors.Count > 0)
        {
            return ValidationFail(validationErrors);
        }

        existing.Name = nameValidation.CanonicalName;
        existing.Emoji = emojiValidation.CanonicalEmoji;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        await scope.SaveChangesAsync();
        await _boardEvents.ResyncRequestedAsync(boardId);

        return existing.ToCardTypeDto();
    }

    public async Task<ApiResult> DeleteCardTypeAsync(int boardId, int cardTypeId, int actorUserId)
    {
        using var scope = _scopeFactory.Create();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardManageSettings);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var existing = await cardTypeRepository.GetByIdInBoardAsync(boardId, cardTypeId);
        if (existing is null)
        {
            return ApiResults.Ok();
        }

        if (existing.IsSystem)
        {
            return ApiErrors.BadRequest("System card type cannot be deleted.");
        }

        var systemCardType = await cardTypeRepository.GetSystemByBoardIdAsync(boardId);
        if (systemCardType is null)
        {
            return ApiErrors.InternalError("System card type not found for board.");
        }

        var now = DateTime.UtcNow;
        var cardsToReassign = await cardRepository.GetByBoardAndCardTypeAsync(boardId, existing.Id);
        foreach (var card in cardsToReassign)
        {
            card.CardTypeId = systemCardType.Id;
            card.UpdatedAtUtc = now;
        }

        cardTypeRepository.Remove(existing);
        await scope.SaveChangesAsync();
        await _boardEvents.ResyncRequestedAsync(boardId);

        return ApiResults.Ok();
    }

    private static ApiError ValidationFail(IReadOnlyList<ValidationError> validationErrors) =>
        ApiErrors.BadRequest("Validation failed.", validationErrors);

    private static CardTypeNameValidationResult ValidateCardTypeName(string? rawName, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return new CardTypeNameValidationResult(string.Empty, string.Empty, new ValidationError(propertyName, "Card type name is required."));
        }

        var canonicalName = rawName.Trim();
        if (canonicalName.Length > MaxCardTypeNameLength)
        {
            return new CardTypeNameValidationResult(
                string.Empty,
                string.Empty,
                new ValidationError(propertyName, $"Card type '{canonicalName}' must be {MaxCardTypeNameLength} characters or fewer."));
        }

        return new CardTypeNameValidationResult(canonicalName, NormaliseName(canonicalName), null);
    }

    private static string NormaliseName(string name) =>
        name.ToUpperInvariant();

    private sealed record CardTypeNameValidationResult(
        string CanonicalName,
        string NormalisedName,
        ValidationError? Error);
}
