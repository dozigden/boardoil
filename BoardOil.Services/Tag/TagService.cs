using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Tag;
using BoardOil.Contracts.Contracts;
using BoardOil.Contracts.Tag;
using BoardOil.Persistence.Abstractions.Board;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Persistence.Abstractions.Tag;

namespace BoardOil.Services.Tag;

public sealed class TagService(
    IBoardRepository boardRepository,
    ITagRepository tagRepository,
    IDbContextScopeFactory scopeFactory) : ITagService
{
    private const int MaxTagNameLength = 40;
    private readonly IDbContextScopeFactory _scopeFactory = scopeFactory;

    public async Task<ApiResult<IReadOnlyList<TagDto>>> GetTagsAsync(int boardId)
    {
        using var scope = _scopeFactory.CreateReadOnly();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var tags = await tagRepository.GetAllForBoardAsync(boardId);
        return tags.Select(x => x.ToTagDto()).ToList();
    }

    public async Task<ApiResult<TagDto>> CreateTagAsync(int boardId, CreateTagRequest request)
    {
        using var scope = _scopeFactory.Create();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
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
            return ValidationFail(createValidationErrors);
        }

        var existing = await tagRepository.GetByNormalisedNameAsync(boardId, tagValidation.NormalisedName);
        if (existing is not null)
        {
            return ApiResults.Ok(existing.ToTagDto());
        }

        var now = DateTime.UtcNow;
        tagRepository.Add(new EntityTag
        {
            BoardId = boardId,
            Name = tagValidation.CanonicalName,
            NormalisedName = tagValidation.NormalisedName,
            StyleName = TagStyleSchemaValidator.SolidStyleName,
            StylePropertiesJson = TagStyleSchemaValidator.BuildDefaultStylePropertiesJson(),
            Emoji = emojiValidation.CanonicalEmoji,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await scope.SaveChangesAsync();

        var created = await tagRepository.GetByNormalisedNameAsync(boardId, tagValidation.NormalisedName);
        if (created is null)
        {
            return ApiErrors.InternalError("Created tag could not be reloaded.");
        }

        return ApiResults.Created(created.ToTagDto());
    }

    public async Task<ApiResult<TagDto>> UpdateTagStyleAsync(int boardId, int tagId, UpdateTagStyleRequest request)
    {
        using var scope = _scopeFactory.Create();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var normalisedStyleName = TagStyleSchemaValidator.NormaliseStyleName(request.StyleName);
        if (normalisedStyleName is null)
        {
            return ValidationFail([new ValidationError("styleName", "Style name must be 'solid' or 'gradient'.")]);
        }

        var emojiValidation = TagEmojiValidator.ValidateAndNormalise(request.Emoji, "emoji");
        var styleValidationErrors = TagStyleSchemaValidator.Validate(normalisedStyleName, request.StylePropertiesJson);
        if (emojiValidation.Error is not null || styleValidationErrors.Count > 0)
        {
            var validationErrors = new List<ValidationError>();
            if (emojiValidation.Error is not null)
            {
                validationErrors.Add(emojiValidation.Error);
            }

            validationErrors.AddRange(styleValidationErrors);
            return ValidationFail(validationErrors);
        }

        var existing = await tagRepository.GetByIdInBoardAsync(boardId, tagId);
        if (existing is null)
        {
            return ApiErrors.NotFound("Tag not found.");
        }

        var updatedAtUtc = DateTime.UtcNow;
        existing.StyleName = normalisedStyleName;
        existing.StylePropertiesJson = request.StylePropertiesJson;
        existing.Emoji = emojiValidation.CanonicalEmoji;
        existing.UpdatedAtUtc = updatedAtUtc;

        await scope.SaveChangesAsync();

        return existing.ToTagDto();
    }

    public async Task<ApiResult> DeleteTagAsync(int boardId, int tagId)
    {
        using var scope = _scopeFactory.Create();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var existing = await tagRepository.GetByIdInBoardAsync(boardId, tagId);
        if (existing is null)
        {
            return ApiResults.Ok();
        }

        tagRepository.Remove(existing);
        await scope.SaveChangesAsync();

        return ApiResults.Ok();
    }

    private static ApiError ValidationFail(IReadOnlyList<ValidationError> validationErrors) =>
        ApiErrors.BadRequest("Validation failed.", validationErrors);

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
