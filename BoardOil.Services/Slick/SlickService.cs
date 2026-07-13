using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Slick;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.Slick;
using BoardOil.Contracts.Style;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Data.Abstractions.Slick;
using BoardOil.Services.Style;
using BoardOil.Services.Tag;

namespace BoardOil.Services.Slick;

public sealed class SlickService(
    IBoardRepository boardRepository,
    ISlickRepository slickRepository,
    IBoardAuthorisationService boardAuthorisationService,
    IBoardEvents boardEvents,
    IBoardStyleDefaultService styleDefaultService,
    IDbContextScopeFactory scopeFactory) : ISlickService
{
    public async Task<ApiResult<IReadOnlyList<SlickDto>>> GetSlicksAsync(int boardId, int actorUserId)
    {
        using var scope = scopeFactory.CreateReadOnly();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardAccess);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have access to this board.");
        }

        var slicks = await slickRepository.GetAllForBoardAsync(boardId);
        return slicks.Select(x => x.ToSlickDto()).ToList();
    }

    public async Task<ApiResult<StyleDefaultDto>> GetCreateDefaultStyleAsync(int boardId, int actorUserId)
    {
        using var scope = scopeFactory.CreateReadOnly();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.TagManage);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        return await styleDefaultService.GetSlickCreateDefaultStyleAsync(boardId);
    }

    public async Task<ApiResult<SlickDto>> CreateSlickAsync(int boardId, CreateSlickRequest request, int actorUserId)
    {
        using var scope = scopeFactory.Create();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.TagManage);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var nameValidation = SlickNameValidation.ValidateRequired(request.Name, "name");
        var validationErrors = new List<ValidationError>();
        if (nameValidation.Error is not null)
        {
            validationErrors.Add(nameValidation.Error);
        }

        if (nameValidation.Error is null)
        {
            var existing = await slickRepository.GetByNormalisedNameAsync(boardId, nameValidation.NormalisedName);
            if (existing is not null)
            {
                validationErrors.Add(new ValidationError("name", $"Slick '{nameValidation.CanonicalName}' already exists."));
            }
        }

        var styleValidation = await ResolveAndValidateCreateStyleAsync(boardId, request.StyleName, request.StylePropertiesJson);
        if (styleValidation.Error is not null)
        {
            validationErrors.Add(styleValidation.Error);
        }

        if (validationErrors.Count > 0 || styleValidation.Error is not null)
        {
            return ApiErrors.ValidationFailed(validationErrors);
        }

        slickRepository.Add(new Data.Abstractions.Entities.EntitySlick
        {
            BoardId = boardId,
            Name = nameValidation.CanonicalName,
            NormalisedName = nameValidation.NormalisedName,
            StyleName = styleValidation.StyleName,
            StylePropertiesJson = styleValidation.StylePropertiesJson,
        });
        await scope.SaveChangesAsync();
        await boardEvents.ResyncRequestedAsync(boardId);

        var created = await slickRepository.GetByNormalisedNameAsync(boardId, nameValidation.NormalisedName);
        if (created is null)
        {
            return ApiErrors.InternalError("Created slick could not be reloaded.");
        }

        return ApiResults.Created(created.ToSlickDto());
    }

    public async Task<ApiResult<SlickDto>> UpdateSlickAsync(int boardId, int slickId, UpdateSlickRequest request, int actorUserId)
    {
        using var scope = scopeFactory.Create();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.TagManage);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var existing = await slickRepository.GetByIdInBoardAsync(boardId, slickId);
        if (existing is null)
        {
            return ApiErrors.NotFound("Slick not found.");
        }

        var nameValidation = SlickNameValidation.ValidateRequired(request.Name, "name");
        var validationErrors = new List<ValidationError>();
        if (nameValidation.Error is not null)
        {
            validationErrors.Add(nameValidation.Error);
        }
        else
        {
            var byName = await slickRepository.GetByNormalisedNameAsync(boardId, nameValidation.NormalisedName);
            if (byName is not null && byName.Id != existing.Id)
            {
                validationErrors.Add(new ValidationError("name", $"Slick '{nameValidation.CanonicalName}' already exists."));
            }
        }

        var styleValidation = ResolveAndValidateStyle(request.StyleName, request.StylePropertiesJson);
        if (styleValidation.Error is not null)
        {
            validationErrors.Add(styleValidation.Error);
        }

        if (validationErrors.Count > 0 || styleValidation.Error is not null)
        {
            return ApiErrors.ValidationFailed(validationErrors);
        }

        existing.Name = nameValidation.CanonicalName;
        existing.NormalisedName = nameValidation.NormalisedName;
        existing.StyleName = styleValidation.StyleName;
        existing.StylePropertiesJson = styleValidation.StylePropertiesJson;

        await scope.SaveChangesAsync();
        await boardEvents.ResyncRequestedAsync(boardId);
        return existing.ToSlickDto();
    }

    public async Task<ApiResult> DeleteSlickAsync(int boardId, int slickId, int actorUserId)
    {
        using var scope = scopeFactory.Create();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.TagManage);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var existing = await slickRepository.GetByIdInBoardAsync(boardId, slickId);
        if (existing is null)
        {
            return ApiResults.Ok();
        }

        slickRepository.Remove(existing);
        await scope.SaveChangesAsync();
        await boardEvents.ResyncRequestedAsync(boardId);
        return ApiResults.Ok();
    }

    private static SlickStyleValidationResult ResolveAndValidateStyle(string? styleName, string? stylePropertiesJson)
    {
        var requestedStyleName = styleName?.Trim();
        var normalisedStyleName = NormaliseSlickStyleName(requestedStyleName ?? TagStyleSchemaValidator.PresetsStyleName);
        if (normalisedStyleName is null)
        {
            return new SlickStyleValidationResult(
                string.Empty,
                string.Empty,
                new ValidationError("styleName", "Style name must be 'solid' or 'presets'."));
        }

        var resolvedStylePropertiesJson = string.IsNullOrWhiteSpace(stylePropertiesJson)
            ? TagStyleSchemaValidator.BuildDefaultStylePropertiesJson(normalisedStyleName)
            : stylePropertiesJson.Trim();
        if (!TagStyleSchemaValidator.IsValidJsonObject(resolvedStylePropertiesJson))
        {
            return new SlickStyleValidationResult(
                string.Empty,
                string.Empty,
                new ValidationError("stylePropertiesJson", "Style properties must be valid JSON object."));
        }

        return new SlickStyleValidationResult(normalisedStyleName, resolvedStylePropertiesJson, null);
    }

    private async Task<SlickStyleValidationResult> ResolveAndValidateCreateStyleAsync(
        int boardId,
        string? styleName,
        string? stylePropertiesJson)
    {
        var hasRequestedStyleName = !string.IsNullOrWhiteSpace(styleName);
        var hasRequestedStyleProperties = !string.IsNullOrWhiteSpace(stylePropertiesJson);
        if (!hasRequestedStyleName && !hasRequestedStyleProperties)
        {
            var defaultStyle = await styleDefaultService.GetSlickCreateDefaultStyleAsync(boardId);
            return new SlickStyleValidationResult(defaultStyle.StyleName, defaultStyle.StylePropertiesJson, null);
        }

        return ResolveAndValidateStyle(styleName, stylePropertiesJson);
    }

    private static string? NormaliseSlickStyleName(string styleName)
    {
        var normalised = TagStyleSchemaValidator.NormaliseStyleName(styleName);
        if (normalised is not TagStyleSchemaValidator.SolidStyleName
            && normalised is not TagStyleSchemaValidator.PresetsStyleName)
        {
            return null;
        }

        return normalised;
    }

    private sealed record SlickStyleValidationResult(
        string StyleName,
        string StylePropertiesJson,
        ValidationError? Error);
}
