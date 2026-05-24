using BoardOil.Abstractions;
using BoardOil.Abstractions.Configuration;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Configuration;
using BoardOil.Contracts.Contracts;
using BoardOil.Data.Abstractions.Configuration;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Tag;

namespace BoardOil.Services.Configuration;

public sealed class SystemInfoMessageService(
    IDbContextScopeFactory scopeFactory,
    ISystemInfoMessageRepository systemInfoMessageRepository,
    IBoardEvents boardEvents) : ISystemInfoMessageService
{
    public async Task<ApiResult<SystemInfoMessageDto?>> GetAsync()
    {
        using var scope = scopeFactory.CreateReadOnly();
        var existing = await systemInfoMessageRepository.GetCurrentAsync();
        if (existing is null)
        {
            return ApiResults.Ok<SystemInfoMessageDto?>(null);
        }

        SystemInfoMessageDto mapped = new(
            existing.Enabled,
            existing.Emoji,
            existing.Title,
            existing.Description,
            existing.StyleName,
            existing.StylePropertiesJson);
        return ApiResults.Ok<SystemInfoMessageDto?>(mapped);
    }

    public async Task<ApiResult<SystemInfoMessageDto?>> UpdateAsync(SystemInfoMessageDto? request)
    {
        var validationResult = Validate(request);
        if (!validationResult.Success)
        {
            return new ApiResult<SystemInfoMessageDto?>(
                false,
                null,
                validationResult.StatusCode,
                validationResult.Message,
                validationResult.ValidationErrors);
        }

        using var scope = scopeFactory.Create();
        var existing = await systemInfoMessageRepository.GetCurrentAsync();

        var changed = false;
        if (request is null)
        {
            if (existing is not null)
            {
                systemInfoMessageRepository.Remove(existing);
                changed = true;
            }
        }
        else if (existing is null)
        {
            systemInfoMessageRepository.Add(new EntitySystemInfoMessage
            {
                Enabled = request.Enabled,
                Emoji = request.Emoji,
                Title = request.Title,
                Description = request.Description,
                StyleName = request.StyleName,
                StylePropertiesJson = request.StylePropertiesJson
            });
            changed = true;
        }
        else if (existing.Enabled != request.Enabled
            || !string.Equals(existing.Emoji, request.Emoji, StringComparison.Ordinal)
            || !string.Equals(existing.Title, request.Title, StringComparison.Ordinal)
            || !string.Equals(existing.Description, request.Description, StringComparison.Ordinal)
            || !string.Equals(existing.StyleName, request.StyleName, StringComparison.Ordinal)
            || !string.Equals(existing.StylePropertiesJson, request.StylePropertiesJson, StringComparison.Ordinal))
        {
            existing.Enabled = request.Enabled;
            existing.Emoji = request.Emoji;
            existing.Title = request.Title;
            existing.Description = request.Description;
            existing.StyleName = request.StyleName;
            existing.StylePropertiesJson = request.StylePropertiesJson;
            changed = true;
        }

        if (changed)
        {
            await scope.SaveChangesAsync();
            await boardEvents.SystemInfoMessageUpdatedAsync(request);
        }

        return ApiResults.Ok(request);
    }

    private static ApiResult Validate(SystemInfoMessageDto? value)
    {
        if (value is null)
        {
            return ApiResults.Ok();
        }

        var normalisedStyleName = TagStyleSchemaValidator.NormaliseStyleName(value.StyleName);
        if (normalisedStyleName is not TagStyleSchemaValidator.AutoStyleName
            && normalisedStyleName is not TagStyleSchemaValidator.PresetsStyleName
            && normalisedStyleName is not TagStyleSchemaValidator.SolidStyleName)
        {
            return ApiErrors.BadRequest("systemInfoMessage.styleName must be auto, presets, or solid.");
        }

        if (string.IsNullOrWhiteSpace(value.StylePropertiesJson)
            || !TagStyleSchemaValidator.IsValidJsonObject(value.StylePropertiesJson))
        {
            return ApiErrors.BadRequest("systemInfoMessage.stylePropertiesJson must be valid JSON object text.");
        }

        if (value.Enabled && string.IsNullOrWhiteSpace(value.Title))
        {
            return ApiErrors.BadRequest("systemInfoMessage.title is required when enabled is true.");
        }

        return ApiResults.Ok();
    }
}
