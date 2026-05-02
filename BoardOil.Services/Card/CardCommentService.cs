using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Contracts;
using BoardOil.Persistence.Abstractions.Card;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Persistence.Abstractions.Image;

namespace BoardOil.Services.Card;

public sealed class CardCommentService(
    ICardRepository cardRepository,
    ICardCommentRepository cardCommentRepository,
    IImageRepository imageRepository,
    IBoardAuthorisationService boardAuthorisationService,
    IDbContextScopeFactory scopeFactory) : ICardCommentService
{
    private const int MaxCommentLength = 4_000;
    private const string UnknownCommentAuthorDisplayName = "Unknown user";
    private readonly IDbContextScopeFactory _scopeFactory = scopeFactory;

    public async Task<ApiResult<IReadOnlyList<CardCommentDto>>> GetCommentsAsync(int boardId, int cardId, int actorUserId)
    {
        using var scope = _scopeFactory.CreateReadOnly();

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardAccess);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have access to this board.");
        }

        var card = await cardRepository.GetWithTagsAndBoardAsync(cardId);
        if (card is null || card.BoardColumn.BoardId != boardId)
        {
            return ApiErrors.NotFound("Card not found.");
        }

        var comments = await cardCommentRepository.GetForCardOrderedAsync(cardId);
        var imageByAuthorUserId = await LoadAuthorImageLookupAsync(comments);
        var displayNameByAuthorUserId = LoadAuthorDisplayNameLookup(comments);
        return comments
            .Select(x => x.ToCardCommentDto(
                ResolveAuthorDisplayName(x, displayNameByAuthorUserId),
                ResolveAuthorImageRelativePath(x, imageByAuthorUserId)))
            .ToList();
    }

    public async Task<ApiResult<CardCommentDto>> CreateCommentAsync(int boardId, int cardId, CreateCardCommentRequest request, int actorUserId)
    {
        using var scope = _scopeFactory.Create();

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.CardUpdate);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var card = await cardRepository.GetWithTagsAndBoardAsync(cardId);
        if (card is null || card.BoardColumn.BoardId != boardId)
        {
            return ApiErrors.NotFound("Card not found.");
        }

        var validationErrors = ValidateCreateRequest(request);
        if (validationErrors.Count > 0)
        {
            return ApiErrors.BadRequest("Validation failed.", validationErrors);
        }

        var comment = new EntityCardComment
        {
            CardId = cardId,
            AuthorUserId = actorUserId,
            Text = request.Text.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
        cardCommentRepository.Add(comment);
        await scope.SaveChangesAsync();

        var savedComment = await cardCommentRepository.GetByIdWithAuthorAsync(comment.Id);
        if (savedComment is null)
        {
            return ApiErrors.InternalError("Created comment could not be reloaded.");
        }

        string? imageRelativePath = null;
        if (savedComment.AuthorUserId.HasValue)
        {
            var image = await imageRepository.GetLatestForEntityAsync(ImageEntityType.UserProfile, savedComment.AuthorUserId.Value);
            imageRelativePath = image?.RelativePath;
        }

        return ApiResults.Created(savedComment.ToCardCommentDto(savedComment.AuthorUser?.DisplayName, imageRelativePath));
    }

    private static IReadOnlyList<ValidationError> ValidateCreateRequest(CreateCardCommentRequest request)
    {
        var errors = new List<ValidationError>();
        var text = request.Text?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            errors.Add(new ValidationError("text", "Comment text is required."));
            return errors;
        }

        if (text.Length > MaxCommentLength)
        {
            errors.Add(new ValidationError("text", $"Comment text must be {MaxCommentLength} characters or fewer."));
        }

        return errors;
    }

    private static IReadOnlyDictionary<int, string> LoadAuthorDisplayNameLookup(IReadOnlyList<EntityCardComment> comments) =>
        comments
            .Where(x => x.AuthorUserId.HasValue && x.AuthorUser is not null)
            .GroupBy(x => x.AuthorUserId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.AuthorUser!.DisplayName).First());

    private static string ResolveAuthorDisplayName(
        EntityCardComment comment,
        IReadOnlyDictionary<int, string> displayNameByAuthorUserId)
    {
        if (!comment.AuthorUserId.HasValue)
        {
            return UnknownCommentAuthorDisplayName;
        }

        return displayNameByAuthorUserId.GetValueOrDefault(comment.AuthorUserId.Value, UnknownCommentAuthorDisplayName);
    }

    private static string? ResolveAuthorImageRelativePath(
        EntityCardComment comment,
        IReadOnlyDictionary<int, string> imageByAuthorUserId)
    {
        if (!comment.AuthorUserId.HasValue)
        {
            return null;
        }

        return imageByAuthorUserId.GetValueOrDefault(comment.AuthorUserId.Value);
    }

    private async Task<IReadOnlyDictionary<int, string>> LoadAuthorImageLookupAsync(IReadOnlyList<EntityCardComment> comments)
    {
        var authorUserIds = comments
            .Where(x => x.AuthorUserId.HasValue)
            .Select(x => x.AuthorUserId!.Value)
            .Distinct()
            .ToArray();
        if (authorUserIds.Length == 0)
        {
            return new Dictionary<int, string>();
        }

        var images = await imageRepository.GetLatestForEntitiesAsync(ImageEntityType.UserProfile, authorUserIds);
        return images.ToDictionary(x => x.EntityId, x => x.RelativePath);
    }
}
