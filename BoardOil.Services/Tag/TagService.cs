using BoardOil.Abstractions.Tag;
using BoardOil.Contracts.Contracts;
using BoardOil.Contracts.Tag;

namespace BoardOil.Services.Tag;

public sealed class TagService(ITagRepository tagRepository) : ITagService
{
    private const int MaxTagNameLength = 40;

    public async Task<ApiResult<IReadOnlyList<TagDto>>> GetTagsAsync()
    {
        var tags = await tagRepository.GetAllAsync();
        return tags.Select(x => x.ToTagDto()).ToList();
    }

    public async Task<ApiResult<TagDto>> CreateTagAsync(CreateTagRequest request)
    {
        var tagValidation = ValidateTagName(request.Name, "name");
        if (tagValidation.Error is not null)
        {
            return ValidationFail([tagValidation.Error]);
        }

        var existing = await tagRepository.GetByNormalisedNameAsync(tagValidation.NormalisedName);
        if (existing is not null)
        {
            return ApiResults.Ok(existing.ToTagDto());
        }

        var now = DateTime.UtcNow;
        var created = await tagRepository.CreateAsync(new CreateTagRecord(
            Name: tagValidation.CanonicalName,
            NormalisedName: tagValidation.NormalisedName,
            StyleName: TagStyleSchemaValidator.SolidStyleName,
            StylePropertiesJson: TagStyleSchemaValidator.BuildDefaultStylePropertiesJson(),
            CreatedAtUtc: now,
            UpdatedAtUtc: now));

        return ApiResults.Created(created.ToTagDto());
    }

    public async Task<ApiResult<TagDto>> UpdateTagStyleAsync(string name, UpdateTagStyleRequest request)
    {
        var tagValidation = ValidateTagName(name, "name");
        if (tagValidation.Error is not null)
        {
            return ValidationFail([tagValidation.Error]);
        }

        var normalisedStyleName = TagStyleSchemaValidator.NormaliseStyleName(request.StyleName);
        if (normalisedStyleName is null)
        {
            return ValidationFail([new ValidationError("styleName", "Style name must be 'solid' or 'gradient'.")]);
        }

        var styleValidationErrors = TagStyleSchemaValidator.Validate(normalisedStyleName, request.StylePropertiesJson);
        if (styleValidationErrors.Count > 0)
        {
            return ValidationFail(styleValidationErrors);
        }

        var existing = await tagRepository.GetByNormalisedNameAsync(tagValidation.NormalisedName);
        if (existing is null || !string.Equals(existing.Name, tagValidation.CanonicalName, StringComparison.Ordinal))
        {
            return ApiErrors.NotFound("Tag not found.");
        }

        var updatedAtUtc = DateTime.UtcNow;
        await tagRepository.UpdateAsync(new UpdateTagRecord(
            Name: existing.Name,
            NormalisedName: existing.NormalisedName,
            StyleName: normalisedStyleName,
            StylePropertiesJson: request.StylePropertiesJson,
            UpdatedAtUtc: updatedAtUtc));

        return new TagDto(
            existing.Name,
            normalisedStyleName,
            request.StylePropertiesJson,
            existing.CreatedAtUtc,
            updatedAtUtc);
    }

    private static ApiError ValidationFail(IReadOnlyList<ValidationError> validationErrors) =>
        ApiErrors.BadRequest(
            "Validation failed.",
            validationErrors
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Property) ? string.Empty : x.Property)
                .ToDictionary(x => x.Key, x => x.Select(y => y.Message).ToArray()));

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
