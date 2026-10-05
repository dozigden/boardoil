using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Card;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Image;

namespace BoardOil.Services.Card;

public sealed class CardCommentService(
    ICardRepository cardRepository,
    ICardCommentRepository cardCommentRepository,
    IImageRepository imageRepository,
    IBoardAuthorisationService boardAuthorisationService,
    IBoardEvents boardEvents,
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

        var card = await cardRepository.GetWithTagsAndBoardAsync(boardId, cardId);
        if (card is null)
        {
            return ApiErrors.NotFound("Card not found.");
        }

        var comments = await cardCommentRepository.GetForCardOrderedAsync(card.Id);
        var imageByAuthorUserId = await LoadAuthorImageLookupAsync(comments);
        var displayNameByAuthorUserId = LoadAuthorDisplayNameLookup(comments);
        return comments
            .Select(x => x.ToCardCommentDto(
                cardId,
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

        var card = await cardRepository.GetWithTagsAndBoardAsync(boardId, cardId);
        if (card is null)
        {
            return ApiErrors.NotFound("Card not found.");
        }

        var validationErrors = ValidateText(request.Text);
        if (validationErrors.Count > 0)
        {
            return ApiErrors.ValidationFailed(validationErrors);
        }

        var comment = new EntityCardComment
        {
            CardId = card.Id,
            AuthorUserId = actorUserId,
            Text = request.Text.Trim(),
            PostedAtUtc = DateTime.UtcNow
        };
        card.CardUpdatedUtc = comment.PostedAtUtc;
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

        var createdComment = savedComment.ToCardCommentDto(cardId, savedComment.AuthorUser?.DisplayName, imageRelativePath);
        var updatedCard = await CardDtoEnrichment.EnrichAssignedUserImageAsync(card.ToCardDto(), imageRepository);
        await boardEvents.CardUpdatedAsync(boardId, updatedCard);
        await boardEvents.CommentCreatedAsync(boardId, createdComment);
        return ApiResults.Created(createdComment);
    }

    public async Task<ApiResult<CardCommentDto>> UpdateCommentAsync(int boardId, int cardId, int commentId, UpdateCardCommentRequest request, int actorUserId)
    {
        using var scope = _scopeFactory.Create();

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.CardUpdate);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var card = await cardRepository.GetWithTagsAndBoardAsync(boardId, cardId);
        if (card is null)
        {
            return ApiErrors.NotFound("Card not found.");
        }

        var comment = await cardCommentRepository.GetByIdWithAuthorAsync(commentId);
        if (comment is null || comment.CardId != card.Id)
        {
            return ApiErrors.NotFound("Comment not found.");
        }

        if (comment.IsSystem || comment.AuthorUserId != actorUserId)
        {
            return ApiErrors.Forbidden("You can only edit your own comments.");
        }

        var validationErrors = ValidateText(request.Text);
        if (validationErrors.Count > 0)
        {
            return ApiErrors.ValidationFailed(validationErrors);
        }

        var text = request.Text.Trim();
        var changed = !string.Equals(comment.Text, text, StringComparison.Ordinal);
        if (changed)
        {
            comment.Text = text;
            comment.EditedAtUtc = DateTime.UtcNow;
            card.CardUpdatedUtc = comment.EditedAtUtc.Value;
            try
            {
                await scope.SaveChangesAsync();
            }
            catch (ConcurrencyException)
            {
                return ApiErrors.NotFound("Comment not found.");
            }
        }

        var image = await imageRepository.GetLatestForEntityAsync(ImageEntityType.UserProfile, actorUserId);
        var updatedComment = comment.ToCardCommentDto(cardId, comment.AuthorUser?.DisplayName, image?.RelativePath);
        if (changed)
        {
            var updatedCard = await CardDtoEnrichment.EnrichAssignedUserImageAsync(card.ToCardDto(), imageRepository);
            await boardEvents.CardUpdatedAsync(boardId, updatedCard);
            await boardEvents.CommentUpdatedAsync(boardId, updatedComment);
        }

        return ApiResults.Ok(updatedComment);
    }

    public async Task<ApiResult> DeleteCommentAsync(int boardId, int cardId, int commentId, int actorUserId)
    {
        using var scope = _scopeFactory.Create();

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.CardUpdate);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var card = await cardRepository.GetWithTagsAndBoardAsync(boardId, cardId);
        if (card is null)
        {
            return ApiResults.Ok();
        }

        var comment = await cardCommentRepository.GetByIdWithAuthorAsync(commentId);
        if (comment is null)
        {
            return ApiResults.Ok();
        }
        if (comment.CardId != card.Id)
        {
            return ApiErrors.NotFound("Comment not found.");
        }

        if ((comment.IsSystem || comment.AuthorUserId != actorUserId)
            && !await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.CommentModerate))
        {
            return ApiErrors.Forbidden("You can only delete your own comments unless you own this board.");
        }

        var deleted = false;
        await scope.Transaction(async (transactionScope, transaction) =>
        {
            var affectedRows = await cardCommentRepository.DeleteByIdAsync(card.Id, commentId);
            if (affectedRows == 0)
            {
                return;
            }

            card.CardUpdatedUtc = DateTime.UtcNow;
            await transactionScope.SaveChangesAsync();
            await transaction.CommitAsync();
            deleted = true;
        });
        if (!deleted)
        {
            return ApiResults.Ok();
        }

        var updatedCard = await CardDtoEnrichment.EnrichAssignedUserImageAsync(card.ToCardDto(), imageRepository);
        await boardEvents.CardUpdatedAsync(boardId, updatedCard);
        await boardEvents.CommentDeletedAsync(boardId, cardId, commentId);
        return ApiResults.Ok();
    }

    private static IReadOnlyList<ValidationError> ValidateText(string? rawText)
    {
        var errors = new List<ValidationError>();
        var text = rawText?.Trim() ?? string.Empty;
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
