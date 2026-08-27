using BoardOil.Abstractions;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Tag;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.Style;
using BoardOil.Contracts.Tag;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Tag;
using BoardOil.Services.Style;

namespace BoardOil.Services.Tag;

public sealed class TagService(
    IBoardRepository boardRepository,
    ITagRepository tagRepository,
    IBoardAuthorisationService boardAuthorisationService,
    IBoardEvents boardEvents,
    IBoardStyleDefaultService styleDefaultService,
    IDbContextScopeFactory scopeFactory) : ITagService
{
    private const int MaxTagNameLength = 40;
    private readonly IDbContextScopeFactory _scopeFactory = scopeFactory;
    private readonly IBoardEvents _boardEvents = boardEvents;

    public async Task<ApiResult<IReadOnlyList<TagDto>>> GetTagsAsync(int boardId, int actorUserId)
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

        var tags = await tagRepository.GetAllForBoardAsync(boardId);
        return tags.Select(x => x.ToTagDto()).ToList();
    }

    public async Task<ApiResult<StyleDefaultDto>> GetCreateDefaultStyleAsync(int boardId, int actorUserId)
    {
        using var scope = _scopeFactory.CreateReadOnly();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.TagManage);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        return await styleDefaultService.GetTagCreateDefaultStyleAsync(boardId);
    }

    public async Task<ApiResult<TagDto>> CreateTagAsync(int boardId, CreateTagRequest request, int actorUserId)
    {
        using var scope = _scopeFactory.Create();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.TagManage);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var tagValidation = ValidateTagName(request.Name, "name");
        var emojiValidation = TagEmojiValidator.ValidateAndNormalise(request.Emoji, "emoji");
        var createValidationErrors = new List<ValidationError>();
        if (tagValidation.Error is not null)
        {
            createValidationErrors.Add(tagValidation.Error);
        }

        if (emojiValidation.Error is not null)
        {
            createValidationErrors.Add(emojiValidation.Error);
        }

        if (createValidationErrors.Count > 0)
        {
            return ApiErrors.ValidationFailed(createValidationErrors);
        }

        var existing = await tagRepository.GetByNormalisedNameAsync(boardId, tagValidation.NormalisedName);
        if (existing is not null)
        {
            return ApiResults.Ok(existing.ToTagDto());
        }

        var defaultStyle = await styleDefaultService.GetTagCreateDefaultStyleAsync(boardId);
        tagRepository.Add(new EntityTag
        {
            BoardId = boardId,
            Name = tagValidation.CanonicalName,
            NormalisedName = tagValidation.NormalisedName,
            StyleName = defaultStyle.StyleName,
            StylePropertiesJson = defaultStyle.StylePropertiesJson,
            Emoji = emojiValidation.CanonicalEmoji,
        });

        await scope.SaveChangesAsync();
        await _boardEvents.ResyncRequestedAsync(boardId);

        var created = await tagRepository.GetByNormalisedNameAsync(boardId, tagValidation.NormalisedName);
        if (created is null)
        {
            return ApiErrors.InternalError("Created tag could not be reloaded.");
        }

        return ApiResults.Created(created.ToTagDto());
    }

    public async Task<ApiResult<TagDto>> UpdateTagStyleAsync(int boardId, int tagId, UpdateTagRequest request, int actorUserId)
    {
        using var scope = _scopeFactory.Create();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.TagManage);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var existing = await tagRepository.GetByIdInBoardAsync(boardId, tagId);
        if (existing is null)
        {
            return ApiErrors.NotFound("Tag not found.");
        }

        var validationErrors = new List<ValidationError>();
        var styleValidation = StyleDefinitionCodec.ParseForWrite(request.StyleName, request.StylePropertiesJson);
        validationErrors.AddRange(styleValidation.ValidationErrors);

        var emojiValidation = TagEmojiValidator.ValidateAndNormalise(request.Emoji, "emoji");
        if (emojiValidation.Error is not null)
        {
            validationErrors.Add(emojiValidation.Error);
        }

        var tagNameValidation = ValidateTagName(request.Name, "name");
        if (tagNameValidation.Error is not null)
        {
            validationErrors.Add(tagNameValidation.Error);
        }
        else
        {
            var byName = await tagRepository.GetByNormalisedNameAsync(boardId, tagNameValidation.NormalisedName);
            if (byName is not null && byName.Id != existing.Id)
            {
                validationErrors.Add(new ValidationError("name", $"Tag '{tagNameValidation.CanonicalName}' already exists."));
            }
        }

        if (validationErrors.Count > 0 || !styleValidation.IsValid)
        {
            return ApiErrors.ValidationFailed(validationErrors);
        }

        var updatedAtUtc = DateTime.UtcNow;
        existing.Name = tagNameValidation.CanonicalName;
        existing.NormalisedName = tagNameValidation.NormalisedName;
        existing.StyleName = styleValidation.StyleName;
        existing.StylePropertiesJson = styleValidation.StylePropertiesJson;
        existing.Emoji = emojiValidation.CanonicalEmoji;

        await scope.SaveChangesAsync();
        await _boardEvents.ResyncRequestedAsync(boardId);

        return existing.ToTagDto();
    }

    public async Task<ApiResult> DeleteTagAsync(int boardId, int tagId, int actorUserId)
    {
        using var scope = _scopeFactory.Create();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.TagManage);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var existing = await tagRepository.GetByIdInBoardAsync(boardId, tagId);
        if (existing is null)
        {
            return ApiResults.Ok();
        }

        tagRepository.Remove(existing);
        await scope.SaveChangesAsync();
        await _boardEvents.ResyncRequestedAsync(boardId);

        return ApiResults.Ok();
    }
    private static TagNameValidationResult ValidateTagName(string? rawName, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return new TagNameValidationResult(string.Empty, string.Empty, new ValidationError(propertyName, "Tag name is required."));
        }

        var canonicalName = rawName.Trim();
        if (canonicalName.Contains(',', StringComparison.Ordinal))
        {
            return new TagNameValidationResult(string.Empty, string.Empty, new ValidationError(propertyName, "Tag name must be a single value."));
        }

        if (canonicalName.Length > MaxTagNameLength)
        {
            return new TagNameValidationResult(
                string.Empty,
                string.Empty,
                new ValidationError(propertyName, $"Tag '{canonicalName}' must be {MaxTagNameLength} characters or fewer."));
        }

        return new TagNameValidationResult(canonicalName, NormaliseTagName(canonicalName), null);
    }

    private static string NormaliseTagName(string tagName) =>
        tagName.ToUpperInvariant();

    private sealed record TagNameValidationResult(
        string CanonicalName,
        string NormalisedName,
        ValidationError? Error);
}
